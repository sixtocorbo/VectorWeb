using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public class NumeracionRangoService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly ILogger<NumeracionRangoService> _logger;

    public NumeracionRangoService(IDbContextFactory<SecretariaDbContext> contextFactory, ILogger<NumeracionRangoService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // --- MÉTODOS DE CONSULTA ---

    public async Task<List<MaeNumeracionRango>> ObtenerRangosAsync()
    {
        return await EjecutarLecturaSeguraAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MaeNumeracionRangos
                .Include(r => r.IdTipoNavigation)
                .Include(r => r.IdOficinaNavigation)
                .OrderByDescending(r => r.Anio).ThenByDescending(r => r.Activo)
                .ToListAsync();
        }, new List<MaeNumeracionRango>(), "obtener rangos");
    }

    public async Task<List<MaeNumeracionBitacora>> ObtenerBitacoraAsync(int cantidad = 200)
    {
        return await EjecutarLecturaSeguraAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MaeNumeracionBitacoras
                .Include(b => b.IdTipoNavigation)
                .Include(b => b.IdOficinaNavigation)
                .Include(b => b.IdUsuarioNavigation)
                .OrderByDescending(b => b.Fecha).Take(cantidad).ToListAsync();
        }, new List<MaeNumeracionBitacora>(), "obtener bitácora");
    }

    public async Task<MaeNumeracionRango?> ObtenerRangoActivoAsync(int idTipo, int idOficina, int anio)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeNumeracionRangos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdTipo == idTipo &&
                                     r.IdOficina == idOficina &&
                                     r.Anio == anio &&
                                     r.Activo == true &&
                                     r.UltimoUtilizado < r.NumeroFin);
    }

    public async Task<List<CupoLibroMayorItem>> ObtenerLibroMayorCuposAsync()
    {
        return await EjecutarLecturaSeguraAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            var cupos = await context.MaeCuposSecretaria.AsNoTracking().Include(c => c.IdTipoNavigation).ToListAsync();
            var consumo = await context.MaeNumeracionRangos.AsNoTracking()
                .GroupBy(r => new { r.IdTipo, r.Anio })
                .Select(g => new { g.Key.IdTipo, g.Key.Anio, Total = g.Sum(x => x.NumeroFin - x.NumeroInicio + 1) })
                .ToListAsync();
            var lookup = consumo.ToDictionary(x => (x.IdTipo, x.Anio), x => x.Total);

            return cupos.Select(c => {
                lookup.TryGetValue((c.IdTipo, c.Anio), out var consumido);
                return new CupoLibroMayorItem
                {
                    Tipo = c.IdTipoNavigation?.Nombre ?? "S/T",
                    Anio = c.Anio,
                    Cantidad = c.Cantidad,
                    Consumido = consumido,
                    Disponible = Math.Max(0, c.Cantidad - consumido),
                    Fecha = c.Fecha
                };
            }).ToList();
        }, new List<CupoLibroMayorItem>(), "obtener libro mayor");
    }

    // --- MÉTODOS DE OPERACIÓN (CON BITÁCORA Y SEGURIDAD) ---

    public async Task<OperacionResultado<MaeNumeracionRango>> ConsumirSiguienteNumeroAsync(int idTipo, int idOficina, int anio)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var rango = await context.MaeNumeracionRangos
                .FirstOrDefaultAsync(r => r.IdTipo == idTipo && r.IdOficina == idOficina && r.Anio == anio && r.Activo);

            if (rango == null) return OperacionResultado<MaeNumeracionRango>.Fail("No hay rango configurado o activo.");
            if (rango.UltimoUtilizado >= rango.NumeroFin) return OperacionResultado<MaeNumeracionRango>.Fail("El rango de numeración se ha agotado.");

            var pId = new SqlParameter("@Id", rango.IdRango);
            var result = await context.Database.SqlQueryRaw<int>(@"
                UPDATE Mae_NumeracionRangos 
                SET UltimoUtilizado = UltimoUtilizado + 1 
                OUTPUT INSERTED.UltimoUtilizado
                WHERE IdRango = @Id AND Activo = 1 AND UltimoUtilizado < NumeroFin", pId).ToListAsync();

            if (!result.Any()) return OperacionResultado<MaeNumeracionRango>.Fail("Error al incrementar el número.");

            rango.UltimoUtilizado = result.First();
            return OperacionResultado<MaeNumeracionRango>.Ok(rango);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al consumir número");
            return OperacionResultado<MaeNumeracionRango>.Fail("Error interno de base de datos.");
        }
    }

    public async Task<OperacionResultado> GuardarRangoAsync(MaeNumeracionRango rango)
    {
        return await EjecutarOperacionControladaAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            string accionBitacora;
            string detalleBitacora;

            if (rango.IdRango == 0)
            {
                context.MaeNumeracionRangos.Add(rango);
                accionBitacora = "APERTURA";
                detalleBitacora = $"Creación de nuevo rango: {rango.NumeroInicio}-{rango.NumeroFin}";
            }
            else
            {
                var dbRango = await context.MaeNumeracionRangos.FindAsync(rango.IdRango);
                if (dbRango == null) return OperacionResultado.Fail("El rango no existe.");

                context.Entry(dbRango).CurrentValues.SetValues(rango);
                accionBitacora = "CAMBIO";
                detalleBitacora = $"Modificación de rango ID {rango.IdRango}. Nuevo fin: {rango.NumeroFin}";
            }

            // REGISTRO EN BITÁCORA (Campo Entidad agregado para evitar SqlException)
            context.MaeNumeracionBitacoras.Add(new MaeNumeracionBitacora
            {
                Fecha = DateTime.Now,
                Entidad = "RANGOS",
                Accion = accionBitacora,
                Detalle = detalleBitacora,
                IdTipo = rango.IdTipo,
                IdOficina = rango.IdOficina
            });

            await context.SaveChangesAsync();
            return OperacionResultado.Ok();
        }, "Error al guardar rango", "guardar rango");
    }

    public async Task<OperacionResultado> EliminarRangoAsync(int idRango)
    {
        return await EjecutarOperacionControladaAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            var rango = await context.MaeNumeracionRangos.FindAsync(idRango);

            if (rango != null)
            {
                context.MaeNumeracionBitacoras.Add(new MaeNumeracionBitacora
                {
                    Fecha = DateTime.Now,
                    Entidad = "RANGOS",
                    Accion = "ELIMINACION",
                    Detalle = $"Se eliminó el rango {rango.NombreRango} ({rango.NumeroInicio}-{rango.NumeroFin})",
                    IdTipo = rango.IdTipo,
                    IdOficina = rango.IdOficina
                });

                context.MaeNumeracionRangos.Remove(rango);
                await context.SaveChangesAsync();
            }
            return OperacionResultado.Ok();
        }, "Error al eliminar", "eliminar rango");
    }

    public async Task<OperacionResultado> GuardarCupoAsync(int idTipo, int anio, int cantidad)
    {
        return await EjecutarOperacionControladaAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            var cupo = await context.MaeCuposSecretaria.FirstOrDefaultAsync(c => c.IdTipo == idTipo && c.Anio == anio);

            if (cupo == null)
            {
                context.MaeCuposSecretaria.Add(new MaeCuposSecretarium
                {
                    IdTipo = idTipo,
                    Anio = anio,
                    Cantidad = cantidad,
                    Fecha = DateTime.Now,
                    NombreCupo = $"CUPO-{idTipo}-{anio}"
                });
            }
            else
            {
                cupo.Cantidad = cantidad;
                cupo.Fecha = DateTime.Now;
            }

            // REGISTRO EN BITÁCORA PARA CUPOS
            context.MaeNumeracionBitacoras.Add(new MaeNumeracionBitacora
            {
                Fecha = DateTime.Now,
                Entidad = "CUPOS",
                Accion = "CONFIGURACION",
                Detalle = $"Se configuró cupo anual de {cantidad} para Tipo {idTipo} / Año {anio}",
                IdTipo = idTipo
            });

            await context.SaveChangesAsync();
            return OperacionResultado.Ok();
        }, "Error al guardar cupo", "guardar cupo");
    }

    public async Task<SugerenciaRangoNumeracion> ObtenerSugerenciaRangoAsync(int idTipo, int anio, int? idOficina, int? idRangoActual, int? cantidadSolicitada)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var cupoTotal = await context.MaeCuposSecretaria.Where(c => c.IdTipo == idTipo && c.Anio == anio).Select(c => (int?)c.Cantidad).FirstOrDefaultAsync() ?? 0;
        var rangosExistentes = await context.MaeNumeracionRangos
            .Where(r => r.IdTipo == idTipo && r.Anio == anio && (!idRangoActual.HasValue || r.IdRango != idRangoActual.Value))
            .OrderBy(r => r.NumeroInicio).ToListAsync();

        var consumoActual = rangosExistentes.Sum(r => r.NumeroFin - r.NumeroInicio + 1);
        var saldo = Math.Max(0, cupoTotal - consumoActual);

        int inicioSugerido = 1;
        foreach (var r in rangosExistentes)
        {
            if (inicioSugerido < r.NumeroInicio) break;
            inicioSugerido = r.NumeroFin + 1;
        }

        int cantidad = Math.Min(cantidadSolicitada ?? 50, saldo);

        return new SugerenciaRangoNumeracion
        {
            CupoTotal = cupoTotal,
            SaldoDisponible = saldo,
            ConsumoTotalAsignado = consumoActual,
            NumeroInicioSugerido = inicioSugerido,
            NumeroFinSugerido = inicioSugerido + cantidad - 1,
            CantidadSugerida = cantidad,
            SugerenciaRecortadaPorSaldo = (cantidad < (cantidadSolicitada ?? 50))
        };
    }

    public async Task<int?> ObtenerCantidadCupoAsync(int idTipo, int anio)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeCuposSecretaria.Where(c => c.IdTipo == idTipo && c.Anio == anio).Select(c => (int?)c.Cantidad).FirstOrDefaultAsync();
    }

    public async Task<int> ObtenerConsumoAcumuladoAsync(int idTipo, int anio)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeNumeracionRangos.Where(r => r.IdTipo == idTipo && r.Anio == anio).SumAsync(r => r.NumeroFin - r.NumeroInicio + 1);
    }

    private async Task<T> EjecutarLecturaSeguraAsync<T>(Func<Task<T>> accion, T fallback, string op)
    {
        try { return await accion(); } catch (Exception ex) { _logger.LogError(ex, "Error en {Op}", op); return fallback; }
    }

    private async Task<OperacionResultado> EjecutarOperacionControladaAsync(Func<Task<OperacionResultado>> accion, string msg, string op)
    {
        try { return await accion(); } catch (Exception ex) { _logger.LogError(ex, "Error en {Op}", op); return OperacionResultado.Fail(msg); }
    }
}

// --- CLASES DE SOPORTE ---

public class OperacionResultado
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public static OperacionResultado Ok() => new() { Exitoso = true };
    public static OperacionResultado Fail(string m) => new() { Exitoso = false, Mensaje = m };
}

public class OperacionResultado<T> : OperacionResultado
{
    public T? Data { get; set; }
    public static OperacionResultado<T> Ok(T d) => new() { Exitoso = true, Data = d };
    public new static OperacionResultado<T> Fail(string m) => new() { Exitoso = false, Mensaje = m };
}

public sealed class CupoLibroMayorItem
{
    public string Tipo { get; set; } = "";
    public int Anio { get; set; }
    public int Cantidad { get; set; }
    public int Consumido { get; set; }
    public int Disponible { get; set; }
    public DateTime Fecha { get; set; }
}

public sealed class SugerenciaRangoNumeracion
{
    public int CupoTotal { get; set; }
    public int ConsumoTotalAsignado { get; set; }
    public int SaldoDisponible { get; set; }
    public int NumeroInicioSugerido { get; set; }
    public int NumeroFinSugerido { get; set; }
    public int CantidadSugerida { get; set; }
    public bool SugerenciaRecortadaPorSaldo { get; set; }
    public bool OficinaTieneRangoActivo { get; set; }
    public bool RangoActivoAgotado { get; set; }
}