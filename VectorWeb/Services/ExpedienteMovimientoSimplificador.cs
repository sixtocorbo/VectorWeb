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
    private const string SinResponsable = "Sin responsable";
    private const string SinOrigen = "Sin origen";
    private const string SinDestino = "Sin destino";
    private const string DocumentoSinNumero = "Documento S/N";
    private const string ObservacionActuacionAgregada = "Actuación agregada";
    private const string PalabraRetorno = "retorno";
    private const string PalabraDestino = "destino";
    private const string ObservacionIngresoAutomatico = "ingreso automático";

    public static List<EventoExpedienteSimplificado> ConstruirEventos(IEnumerable<TraMovimiento> movimientos, long idDocumentoPrincipal)
    {
        var depurados = movimientos
            .Where(m => m.FechaMovimiento.HasValue && !EsActuacionTecnicaDeDocumentoVinculado(m, idDocumentoPrincipal));

        return depurados
            .GroupBy(m => new
            {
                Fecha = TruncarAlMinuto(m.FechaMovimiento!.Value),
                m.IdOficinaOrigen,
                OficinaOrigenNombre = m.IdOficinaOrigenNavigation?.Nombre,
                m.IdOficinaDestino,
                OficinaDestinoNombre = m.IdOficinaDestinoNavigation?.Nombre,
                Observacion = (m.ObservacionPase ?? string.Empty).Trim(),
                Responsable = m.IdUsuarioResponsableNavigation?.NombreCompleto ?? SinResponsable
            })
            .Select(g =>
            {
                var origen = g.Key.OficinaOrigenNombre ?? SinOrigen;
                var destino = g.Key.OficinaDestinoNombre ?? SinDestino;
                var observacion = g.Key.Observacion;

                var documentos = g
                    .OrderBy(m => m.IdDocumento == idDocumentoPrincipal ? 0 : 1)
                    .ThenBy(m => m.IdDocumentoNavigation?.IdTipoNavigation?.Nombre)
                    .ThenBy(m => ObtenerNumeroMostrable(m.IdDocumentoNavigation))
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

    private static DateTime TruncarAlMinuto(DateTime fecha)
    {
        return fecha.AddTicks(-(fecha.Ticks % TimeSpan.TicksPerMinute));
    }

    private static bool EsActuacionTecnicaDeDocumentoVinculado(TraMovimiento movimiento, long idDocumentoPrincipal)
    {
        var esActuacion = string.Equals(movimiento.ObservacionPase?.Trim(), ObservacionActuacionAgregada, StringComparison.OrdinalIgnoreCase);
        return esActuacion
            && movimiento.IdOficinaOrigen == movimiento.IdOficinaDestino
            && movimiento.IdDocumento != idDocumentoPrincipal;
    }

    private static string FormatearDocumento(MaeDocumento? documento)
    {
        var tipo = documento?.IdTipoNavigation?.Nombre?.Trim();
        var (numero, esNumeroInterno) = ObtenerNumeroMostrable(documento);

        if (string.IsNullOrWhiteSpace(tipo) && string.IsNullOrWhiteSpace(numero)) return DocumentoSinNumero;
        if (string.IsNullOrWhiteSpace(tipo)) return esNumeroInterno ? $"ID {numero}" : numero!;
        if (string.IsNullOrWhiteSpace(numero)) return $"{tipo} S/N";
        return esNumeroInterno ? $"{tipo} (ID {numero})" : $"{tipo} {numero}";
    }

    private static (string? Numero, bool EsNumeroInterno) ObtenerNumeroMostrable(MaeDocumento? documento)
    {
        if (documento is null) return (null, false);

        var numeroOficial = documento.NumeroOficial?.Trim();
        if (!string.IsNullOrWhiteSpace(numeroOficial)) return (numeroOficial, false);

        var numeroInterno = documento.NumeroInterno?.Trim();
        if (!string.IsNullOrWhiteSpace(numeroInterno)) return (numeroInterno, true);

        return (null, false);
    }

    private static string ConstruirResumenUsuario(string origen, string destino, string observacion, bool esActuacionInterna)
    {
        if (esActuacionInterna)
        {
            return $"Se registró una actuación en {origen}.";
        }

        if (observacion.Contains(PalabraRetorno, StringComparison.OrdinalIgnoreCase))
        {
            return $"Se recibió el expediente desde {origen}.";
        }

        if (observacion.Contains(PalabraDestino, StringComparison.OrdinalIgnoreCase))
        {
            return $"Se envió el expediente a {destino}.";
        }

        if (observacion.Contains(ObservacionIngresoAutomatico, StringComparison.OrdinalIgnoreCase))
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
