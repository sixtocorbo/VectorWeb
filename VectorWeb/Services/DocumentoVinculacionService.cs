using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class DocumentoVinculacionService
{
    private readonly SecretariaDbContext context;

    public DocumentoVinculacionService(SecretariaDbContext context)
    {
        this.context = context;
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

        await using var tx = await context.Database.BeginTransactionAsync();

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

            if (GeneraCiclo(idPadre, idHijo, padresDict))
            {
                detalles.Add(new VinculacionDetalle(idHijo, "Operación omitida: generaría una relación circular.", 0, false));
                continue;
            }

            var idsSubarbol = ObtenerSubarbol(idHijo, relacionesHijos);
            subarbolPorHijo[idHijo] = idsSubarbol;
            var cantidadDescendientes = Math.Max(0, idsSubarbol.Count - 1);

            var mensajeEstado = idPadreActualHijo.HasValue
                ? $"Reasignado desde padre #{idPadreActualHijo.Value}."
                : "Vinculado sin padre previo.";

            foreach (var id in idsSubarbol)
            {
                idsAModificar.Add(id);
            }

            detalles.Add(new VinculacionDetalle(idHijo, mensajeEstado, cantidadDescendientes, true));
        }

        if (idsAModificar.Count == 0)
        {
            return VinculacionResultado.ConError("No se aplicaron cambios. Revise las validaciones de consistencia.", detalles);
        }

        idsAModificar.Add(idPadre);

        var documentosParaActualizar = await context.MaeDocumentos
            .Where(d => idsAModificar.Contains(d.IdDocumento))
            .ToDictionaryAsync(d => d.IdDocumento);

        if (!documentosParaActualizar.TryGetValue(idPadre, out var padre))
        {
            return VinculacionResultado.ConError("No se encontró el documento padre seleccionado.");
        }

        foreach (var idHijo in hijosSolicitados.Where(id => subarbolPorHijo.ContainsKey(id)))
        {
            if (!documentosParaActualizar.TryGetValue(idHijo, out var hijo))
            {
                continue;
            }

            hijo.IdDocumentoPadre = idPadre;

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

    private static bool GeneraCiclo(long idPadre, long idCandidatoHijo, IReadOnlyDictionary<long, long?> padresDict)
    {
        var cursor = idPadre;

        while (padresDict.TryGetValue(cursor, out var idDocumentoPadre) && idDocumentoPadre.HasValue)
        {
            if (idDocumentoPadre.Value == idCandidatoHijo)
            {
                return true;
            }

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
            if (!visitados.Add(actual))
            {
                continue;
            }

            if (!relacionesHijos.TryGetValue(actual, out var hijos))
            {
                continue;
            }

            foreach (var hijo in hijos)
            {
                pila.Push(hijo);
            }
        }

        return visitados.ToList();
    }
}

public sealed record VinculacionDetalle(long IdDocumento, string Estado, int CantidadDescendientes, bool Aplicado);

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
