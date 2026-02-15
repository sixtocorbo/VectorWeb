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

        var documentos = await context.MaeDocumentos
            .ToListAsync();

        var porId = documentos.ToDictionary(d => d.IdDocumento);

        if (!porId.TryGetValue(idPadre, out var padre))
        {
            return VinculacionResultado.ConError("No se encontró el documento padre seleccionado.");
        }

        var relacionesHijos = documentos
            .GroupBy(d => d.IdDocumentoPadre)
            .ToDictionary(g => g.Key, g => g.Select(x => x.IdDocumento).ToList());

        var detalles = new List<VinculacionDetalle>();
        var modificados = new HashSet<long>();

        foreach (var idHijo in hijosSolicitados)
        {
            if (!porId.TryGetValue(idHijo, out var hijo))
            {
                detalles.Add(new VinculacionDetalle(idHijo, "Documento inexistente.", 0, false));
                continue;
            }

            if (GeneraCiclo(idPadre, idHijo, porId))
            {
                detalles.Add(new VinculacionDetalle(idHijo, "Operación omitida: generaría una relación circular.", 0, false));
                continue;
            }

            var idsSubarbol = ObtenerSubarbol(idHijo, relacionesHijos);
            var cantidadDescendientes = Math.Max(0, idsSubarbol.Count - 1);

            var mensajeEstado = hijo.IdDocumentoPadre.HasValue
                ? $"Reasignado desde padre #{hijo.IdDocumentoPadre.Value}."
                : "Vinculado sin padre previo.";

            hijo.IdDocumentoPadre = idPadre;
            modificados.Add(hijo.IdDocumento);

            foreach (var id in idsSubarbol)
            {
                if (!porId.TryGetValue(id, out var itemSubarbol))
                {
                    continue;
                }

                itemSubarbol.IdHiloConversacion = padre.IdHiloConversacion;
                modificados.Add(id);
            }

            detalles.Add(new VinculacionDetalle(idHijo, mensajeEstado, cantidadDescendientes, true));
        }

        if (modificados.Count == 0)
        {
            return VinculacionResultado.ConError("No se aplicaron cambios. Revise las validaciones de consistencia.", detalles);
        }

        await context.SaveChangesAsync();
        await tx.CommitAsync();

        return VinculacionResultado.Ok(detalles, modificados.Count);
    }

    private static bool GeneraCiclo(long idPadre, long idCandidatoHijo, IReadOnlyDictionary<long, MaeDocumento> porId)
    {
        var cursor = idPadre;

        while (porId.TryGetValue(cursor, out var actual) && actual.IdDocumentoPadre.HasValue)
        {
            if (actual.IdDocumentoPadre.Value == idCandidatoHijo)
            {
                return true;
            }

            cursor = actual.IdDocumentoPadre.Value;
        }

        return false;
    }

    private static List<long> ObtenerSubarbol(long idRaiz, IReadOnlyDictionary<long?, List<long>> relacionesHijos)
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
