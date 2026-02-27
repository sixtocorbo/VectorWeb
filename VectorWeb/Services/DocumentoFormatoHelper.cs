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
}
