using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class DocumentoVinculacionService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;

    public DocumentoVinculacionService(IDbContextFactory<SecretariaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<VinculacionResultado> VincularAsync(long idPadre, IEnumerable<long> idsHijos)
    {
        var hijosSolicitados = (idsHijos ?? Enumerable.Empty<long>())
            .Where(id => id > 0)
            .Distinct()
            .Where(id => id != idPadre)
            .ToList();

        if (hijosSolicitados.Count == 0)
        {
            return VinculacionResultado.ConError("Debe seleccionar al menos un documento hijo para vincular.");
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();

        try
        {
            // Cargamos el esqueleto para validar ciclos y subárboles
            var esqueletoRelacional = await context.MaeDocumentos
                .AsNoTracking()
                .Select(d => new { d.IdDocumento, d.IdDocumentoPadre })
                .ToListAsync();

            var padresDict = esqueletoRelacional.ToDictionary(d => d.IdDocumento, d => d.IdDocumentoPadre);

            if (!padresDict.ContainsKey(idPadre))
            {
                return VinculacionResultado.ConError("No se encontró el documento padre seleccionado.");
            }

            var relacionesHijos = esqueletoRelacional
                .Where(d => d.IdDocumentoPadre.HasValue)
                .GroupBy(d => d.IdDocumentoPadre!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IdDocumento).ToList());

            var detalles = new List<VinculacionDetalle>();
            var idsAModificar = new HashSet<long>();
            var subarbolPorHijo = new Dictionary<long, List<long>>();

            foreach (var idHijo in hijosSolicitados)
            {
                if (!padresDict.TryGetValue(idHijo, out var idPadreActualHijo))
                {
                    detalles.Add(new VinculacionDetalle(idHijo, "Documento inexistente.", 0, false));
                    continue;
                }

                // Validación anti-ciclos
                if (GeneraCiclo(idPadre, idHijo, padresDict))
                {
                    detalles.Add(new VinculacionDetalle(idHijo, "Operación omitida: generaría una relación circular.", 0, false));
                    continue;
                }

                var idsSubarbol = ObtenerSubarbol(idHijo, relacionesHijos);
                subarbolPorHijo[idHijo] = idsSubarbol;

                foreach (var id in idsSubarbol) idsAModificar.Add(id);

                var mensajeEstado = idPadreActualHijo.HasValue
                    ? $"Reasignado desde padre #{idPadreActualHijo.Value}."
                    : "Vinculado sin padre previo.";

                detalles.Add(new VinculacionDetalle(idHijo, mensajeEstado, idsSubarbol.Count - 1, true));
            }

            if (idsAModificar.Count == 0)
            {
                return VinculacionResultado.ConError("No se aplicaron cambios. Revise las validaciones.", detalles);
            }

            idsAModificar.Add(idPadre);

            // Obtenemos los documentos reales para actualizar
            var documentosParaActualizar = await context.MaeDocumentos
                .Where(d => idsAModificar.Contains(d.IdDocumento))
                .ToDictionaryAsync(d => d.IdDocumento);

            if (!documentosParaActualizar.TryGetValue(idPadre, out var padre))
            {
                return VinculacionResultado.ConError("No se encontró el documento padre.");
            }

            foreach (var idHijo in hijosSolicitados.Where(id => subarbolPorHijo.ContainsKey(id)))
            {
                if (!documentosParaActualizar.TryGetValue(idHijo, out var hijo)) continue;

                hijo.IdDocumentoPadre = idPadre;

                // Propagación del Hilo de Conversación a toda la rama descendiente
                foreach (var idDescendiente in subarbolPorHijo[idHijo])
                {
                    if (documentosParaActualizar.TryGetValue(idDescendiente, out var desc))
                    {
                        desc.IdHiloConversacion = padre.IdHiloConversacion;
                    }
                }
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();

            return VinculacionResultado.Ok(detalles, idsAModificar.Count - 1);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return VinculacionResultado.ConError($"Error técnico durante la vinculación: {ex.Message}");
        }
    }

    private static bool GeneraCiclo(long idPadre, long idCandidatoHijo, IReadOnlyDictionary<long, long?> padresDict)
    {
        var cursor = idPadre;
        while (padresDict.TryGetValue(cursor, out var idDocumentoPadre) && idDocumentoPadre.HasValue)
        {
            if (idDocumentoPadre.Value == idCandidatoHijo) return true;
            cursor = idDocumentoPadre.Value;
        }
        return false;
    }

    private static List<long> ObtenerSubarbol(long idRaiz, IReadOnlyDictionary<long, List<long>> relacionesHijos)
    {
        var visitados = new HashSet<long>();
        var pila = new Stack<long>();
        pila.Push(idRaiz);

        while (pila.Count > 0)
        {
            var actual = pila.Pop();
            if (!visitados.Add(actual)) continue;

            if (relacionesHijos.TryGetValue(actual, out var hijos))
            {
                foreach (var hijo in hijos) pila.Push(hijo);
            }
        }
        return visitados.ToList();
    }
}

// --- CLASES DE SOPORTE (MODELOS) ---

public sealed record VinculacionDetalle(
    long IdDocumento,
    string Estado,
    int CantidadDescendientes,
    bool Aplicado
);

public sealed class VinculacionResultado
{
    public bool Exitoso { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public int TotalRegistrosAfectados { get; init; }
    public IReadOnlyCollection<VinculacionDetalle> Detalles { get; init; } = [];

    public static VinculacionResultado Ok(IEnumerable<VinculacionDetalle> detalles, int totalRegistrosAfectados)
        => new()
        {
            Exitoso = true,
            Mensaje = "Vinculación ejecutada correctamente.",
            TotalRegistrosAfectados = totalRegistrosAfectados,
            Detalles = detalles.ToList()
        };

    public static VinculacionResultado ConError(string mensaje, IEnumerable<VinculacionDetalle>? detalles = null)
        => new()
        {
            Exitoso = false,
            Mensaje = mensaje,
            Detalles = detalles?.ToList() ?? []
        };
}