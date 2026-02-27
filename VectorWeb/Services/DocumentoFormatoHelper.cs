using VectorWeb.Models;

namespace VectorWeb.Services;

public static class DocumentoFormatoHelper
{
    private const string DocumentoSinNumero = "Documento S/N";

    public static string FormatearDocumento(MaeDocumento? documento)
    {
        var tipo = documento?.IdTipoNavigation?.Nombre?.Trim();
        var (numero, esNumeroInterno) = ObtenerNumeroMostrable(documento);

        var hasTipo = !string.IsNullOrWhiteSpace(tipo);
        var hasNumero = !string.IsNullOrWhiteSpace(numero);

        return (hasTipo, hasNumero) switch
        {
            (false, false) => DocumentoSinNumero,
            (false, true) => esNumeroInterno ? $"ID {numero}" : numero!,
            (true, false) => $"{tipo} S/N",
            (true, true) => esNumeroInterno ? $"{tipo} (ID {numero})" : $"{tipo} {numero}"
        };
    }

    public static (string? Numero, bool EsNumeroInterno) ObtenerNumeroMostrable(MaeDocumento? documento)
    {
        if (documento is null) return (null, false);

        var numeroOficial = documento.NumeroOficial?.Trim();
        if (!string.IsNullOrWhiteSpace(numeroOficial)) return (numeroOficial, false);

        var numeroInterno = documento.NumeroInterno?.Trim();
        if (!string.IsNullOrWhiteSpace(numeroInterno)) return (numeroInterno, true);

        return (null, false);
    }

    public static string? NormalizarNumeroOficial(string? numeroOficial, int anioReferencia)
    {
        if (string.IsNullOrWhiteSpace(numeroOficial)) return null;

        var numeroLimpio = numeroOficial.Trim();
        var indiceSeparador = numeroLimpio.IndexOf('/');
        var anioCorto = ObtenerAnioCorto(anioReferencia);

        if (indiceSeparador < 0)
        {
            return $"{numeroLimpio}/{anioCorto}";
        }

        var prefijo = numeroLimpio[..indiceSeparador].Trim();
        if (string.IsNullOrWhiteSpace(prefijo)) return numeroLimpio;

        var sufijo = numeroLimpio[(indiceSeparador + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(sufijo))
        {
            return $"{prefijo}/{anioCorto}";
        }

        if (int.TryParse(sufijo, out var anioNumerico))
        {
            if (sufijo.Length == 4)
            {
                return $"{prefijo}/{ObtenerAnioCorto(anioNumerico)}";
            }

            if (sufijo.Length <= 2)
            {
                return $"{prefijo}/{anioNumerico:00}";
            }
        }

        return $"{prefijo}/{sufijo}";
    }

    private static string ObtenerAnioCorto(int anio) => (Math.Abs(anio) % 100).ToString("00");
}
