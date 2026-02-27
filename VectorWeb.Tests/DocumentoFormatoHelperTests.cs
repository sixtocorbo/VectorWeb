using VectorWeb.Services;

namespace VectorWeb.Tests;

public class DocumentoFormatoHelperTests
{
    [Theory]
    [InlineData("120", 2026, "120/26")]
    [InlineData("120/", 2026, "120/26")]
    [InlineData("120/2026", 2026, "120/26")]
    [InlineData("120/26", 2026, "120/26")]
    [InlineData("120/6", 2026, "120/06")]
    [InlineData(" MEM-351 / 2026 ", 2026, "MEM-351/26")]
    public void NormalizarNumeroOficial_NormalizaSegunRegla(string entrada, int anioReferencia, string esperado)
    {
        var resultado = DocumentoFormatoHelper.NormalizarNumeroOficial(entrada, anioReferencia);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void NormalizarNumeroOficial_SinNumero_RetornaNull()
    {
        var resultado = DocumentoFormatoHelper.NormalizarNumeroOficial("   ", 2026);

        Assert.Null(resultado);
    }
}
