using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class EventoExpedienteSimplificado
{
    public DateTime FechaMovimiento { get; init; }
    public int IdOficinaOrigen { get; init; }
    public int IdOficinaDestino { get; init; }
    public string OficinaOrigen { get; init; } = "Sin origen";
    public string OficinaDestino { get; init; } = "Sin destino";
    public string ObservacionOriginal { get; init; } = string.Empty;
    public string ResumenUsuario { get; init; } = string.Empty;
    public string Responsable { get; init; } = "Sin responsable";
    public IReadOnlyList<string> Documentos { get; init; } = [];
}

public static class ExpedienteMovimientoSimplificador
{
    public static List<EventoExpedienteSimplificado> ConstruirEventos(IEnumerable<TraMovimiento> movimientos, long idDocumentoPrincipal)
    {
        var depurados = movimientos
            .Where(m => !EsActuacionTecnicaDeDocumentoVinculado(m, idDocumentoPrincipal));

        return depurados
            .GroupBy(m => new
            {
                Fecha = m.FechaMovimiento ?? DateTime.MinValue,
                m.IdOficinaOrigen,
                m.IdOficinaDestino,
                Observacion = (m.ObservacionPase ?? string.Empty).Trim(),
                Responsable = m.IdUsuarioResponsableNavigation?.NombreCompleto ?? "Sin responsable"
            })
            .Select(g =>
            {
                var origen = g.First().IdOficinaOrigenNavigation?.Nombre ?? "Sin origen";
                var destino = g.First().IdOficinaDestinoNavigation?.Nombre ?? "Sin destino";
                var observacion = g.Key.Observacion;

                var documentos = g
                    .OrderBy(m => m.IdDocumento == idDocumentoPrincipal ? 0 : 1)
                    .ThenBy(m => m.IdDocumentoNavigation?.IdTipoNavigation?.Nombre)
                    .ThenBy(m => m.IdDocumentoNavigation?.NumeroOficial)
                    .Select(m => FormatearDocumento(m.IdDocumentoNavigation))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new EventoExpedienteSimplificado
                {
                    FechaMovimiento = g.Key.Fecha,
                    IdOficinaOrigen = g.Key.IdOficinaOrigen,
                    IdOficinaDestino = g.Key.IdOficinaDestino,
                    OficinaOrigen = origen,
                    OficinaDestino = destino,
                    ObservacionOriginal = observacion,
                    Responsable = g.Key.Responsable,
                    ResumenUsuario = ConstruirResumenUsuario(origen, destino, observacion, g.Key.IdOficinaOrigen == g.Key.IdOficinaDestino),
                    Documentos = documentos
                };
            })
            .OrderByDescending(e => e.FechaMovimiento)
            .ToList();
    }

    private static bool EsActuacionTecnicaDeDocumentoVinculado(TraMovimiento movimiento, long idDocumentoPrincipal)
    {
        var esActuacion = string.Equals(movimiento.ObservacionPase?.Trim(), "Actuación agregada", StringComparison.OrdinalIgnoreCase);
        return esActuacion
            && movimiento.IdOficinaOrigen == movimiento.IdOficinaDestino
            && movimiento.IdDocumento != idDocumentoPrincipal;
    }

    private static string FormatearDocumento(MaeDocumento? documento)
    {
        var tipo = documento?.IdTipoNavigation?.Nombre?.Trim();
        var numero = documento?.NumeroOficial?.Trim();

        if (string.IsNullOrWhiteSpace(tipo) && string.IsNullOrWhiteSpace(numero)) return "Documento S/N";
        if (string.IsNullOrWhiteSpace(tipo)) return numero!;
        if (string.IsNullOrWhiteSpace(numero)) return tipo;
        return $"{tipo} {numero}";
    }

    private static string ConstruirResumenUsuario(string origen, string destino, string observacion, bool esActuacionInterna)
    {
        if (esActuacionInterna)
        {
            return $"Se registró una actuación en {origen}.";
        }

        if (observacion.Contains("retorno", StringComparison.OrdinalIgnoreCase))
        {
            return $"Se recibió el expediente desde {origen}.";
        }

        if (observacion.Contains("destino", StringComparison.OrdinalIgnoreCase))
        {
            return $"Se envió el expediente a {destino}.";
        }

        if (observacion.Contains("ingreso automático", StringComparison.OrdinalIgnoreCase))
        {
            return $"El expediente ingresó a {destino}.";
        }

        if (string.IsNullOrWhiteSpace(observacion))
        {
            return $"Pase de {origen} a {destino}.";
        }

        return observacion;
    }
}
