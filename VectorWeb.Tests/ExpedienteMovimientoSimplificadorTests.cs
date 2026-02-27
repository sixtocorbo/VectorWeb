using VectorWeb.Models;
using VectorWeb.Services;

namespace VectorWeb.Tests;

public class ExpedienteMovimientoSimplificadorTests
{
    [Fact]
    public void ConstruirEventos_NoAgrupaMovimientosDeDistintosSegundos()
    {
        var docPrincipal = CrearDocumento(1, "OFICIO", "100/26");

        var movimientos = new[]
        {
            CrearMovimiento(10, 1, new DateTime(2026, 2, 27, 7, 59, 10), "Traslado automático destino", "BANDEJA DE ENTRADA", "OGLAST", docPrincipal),
            CrearMovimiento(11, 1, new DateTime(2026, 2, 27, 7, 59, 55), "Traslado automático destino", "BANDEJA DE ENTRADA", "OGLAST", docPrincipal)
        };

        var eventos = ExpedienteMovimientoSimplificador.ConstruirEventos(movimientos, idDocumentoPrincipal: 1);

        Assert.Equal(2, eventos.Count);
    }

    [Fact]
    public void ConstruirEventos_OrdenaConDesempatePorUltimoIdMovimiento()
    {
        var docPrincipal = CrearDocumento(1, "OFICIO", "100/26");

        var movimientos = new[]
        {
            CrearMovimiento(100, 1, new DateTime(2026, 2, 27, 7, 59, 10), "Traslado automático destino", "BANDEJA DE ENTRADA", "OGLAST", docPrincipal),
            CrearMovimiento(101, 1, new DateTime(2026, 2, 27, 7, 59, 10), "Traslado automático retorno", "OGLAST", "BANDEJA DE ENTRADA", docPrincipal)
        };

        var eventos = ExpedienteMovimientoSimplificador.ConstruirEventos(movimientos, idDocumentoPrincipal: 1);

        Assert.Equal("Se recibió el expediente desde OGLAST.", eventos[0].ResumenUsuario);
        Assert.Equal(101, eventos[0].UltimoIdMovimiento);
    }

    private static TraMovimiento CrearMovimiento(
        long idMovimiento,
        long idDocumento,
        DateTime fecha,
        string observacion,
        string origen,
        string destino,
        MaeDocumento documento)
    {
        return new TraMovimiento
        {
            IdMovimiento = idMovimiento,
            IdDocumento = idDocumento,
            FechaMovimiento = fecha,
            ObservacionPase = observacion,
            IdOficinaOrigenNavigation = new CatOficina { Nombre = origen },
            IdOficinaDestinoNavigation = new CatOficina { Nombre = destino },
            IdUsuarioResponsableNavigation = new CatUsuario { NombreCompleto = "Administrador" },
            IdDocumentoNavigation = documento
        };
    }

    private static MaeDocumento CrearDocumento(long idDocumento, string tipo, string numero)
    {
        return new MaeDocumento
        {
            IdDocumento = idDocumento,
            NumeroOficial = numero,
            IdTipoNavigation = new CatTipoDocumento { Nombre = tipo }
        };
    }
}
