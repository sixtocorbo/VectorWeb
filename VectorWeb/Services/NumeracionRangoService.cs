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
            .Where(r => r.IdTipo == idTipo &&
                        r.IdOficina == idOficina &&
                        r.Anio == anio &&
                        r.Activo == true &&
                        r.UltimoUtilizado < r.NumeroFin)
            .OrderBy(r => r.NumeroInicio)
            .FirstOrDefaultAsync();
    }

    public async Task<List<MaeNumeracionRango>> ObtenerRangosActivosAsync(int idTipo, int idOficina, int anio)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeNumeracionRangos
            .AsNoTracking()
            .Where(r => r.IdTipo == idTipo &&
                        r.IdOficina == idOficina &&
                        r.Anio == anio &&
                        r.Activo)
            .OrderBy(r => r.NumeroInicio)
            .ToListAsync();
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
                    IdTipo = c.IdTipo,
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
            var rangos = await context.MaeNumeracionRangos
                .Where(r => r.IdTipo == idTipo && r.IdOficina == idOficina && r.Anio == anio && r.Activo)
                .OrderBy(r => r.NumeroInicio)
                .ToListAsync();

            foreach (var rango in rangos)
            {
                if (rango.UltimoUtilizado >= rango.NumeroFin) continue;

                var pId = new SqlParameter("@Id", rango.IdRango);
                var result = await context.Database.SqlQueryRaw<int>(@"
                    UPDATE Mae_NumeracionRangos 
                    SET UltimoUtilizado = UltimoUtilizado + 1 
                    OUTPUT INSERTED.UltimoUtilizado
                    WHERE IdRango = @Id AND Activo = 1 AND UltimoUtilizado < NumeroFin", pId).ToListAsync();

                if (!result.Any()) continue;

                rango.UltimoUtilizado = result.First();
                return OperacionResultado<MaeNumeracionRango>.Ok(rango);
            }

            return OperacionResultado<MaeNumeracionRango>.Fail("No hay rango configurado o activo.");
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

            var cupo = await context.MaeCuposSecretaria
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdTipo == rango.IdTipo && c.Anio == rango.Anio);

            if (cupo == null)
            {
                return OperacionResultado.Fail("No existe cupo configurado para este tipo y año.");
            }

            var cantidadNuevoRango = rango.NumeroFin - rango.NumeroInicio + 1;
            if (cantidadNuevoRango <= 0)
            {
                return OperacionResultado.Fail("Rango inválido (Inicio > Fin).");
            }

            var consumoSinRangoActual = await context.MaeNumeracionRangos
                .Where(r => r.IdTipo == rango.IdTipo && r.Anio == rango.Anio && r.IdRango != rango.IdRango)
                .SumAsync(r => r.NumeroFin - r.NumeroInicio + 1);

            if (consumoSinRangoActual + cantidadNuevoRango > cupo.Cantidad)
            {
                return OperacionResultado.Fail("El rango supera el cupo disponible para este tipo y año.");
            }

            var hayTraslape = await context.MaeNumeracionRangos.AnyAsync(r =>
                r.IdTipo == rango.IdTipo &&
                r.Anio == rango.Anio &&
                r.IdRango != rango.IdRango &&
                rango.NumeroInicio <= r.NumeroFin &&
                rango.NumeroFin >= r.NumeroInicio);

            if (hayTraslape)
            {
                return OperacionResultado.Fail("El rango se superpone con otro ya existente para este tipo y año.");
            }

            string accionBitacora;
            string detalleBitacora;

            async Task<bool> ExisteSolapamientoAsync(int numeroInicio, int numeroFin, int? excluirIdRango)
            {
                return await context.MaeNumeracionRangos.AnyAsync(r =>
                    r.IdTipo == rango.IdTipo &&
                    r.Anio == rango.Anio &&
                    (!excluirIdRango.HasValue || r.IdRango != excluirIdRango.Value) &&
                    numeroInicio <= r.NumeroFin &&
                    numeroFin >= r.NumeroInicio);
            }

            const int maximoActivosPorOficina = 1;

            var existeSolapamiento = await ExisteSolapamientoAsync(rango.NumeroInicio, rango.NumeroFin, rango.IdRango == 0 ? null : rango.IdRango);

            if (existeSolapamiento)
                return OperacionResultado.Fail("El rango se superpone con otro rango ya asignado para este tipo y año.");

            if (rango.Activo && rango.IdOficina.HasValue)
            {
                var cantidadActivosOficina = await context.MaeNumeracionRangos.CountAsync(r =>
                    r.IdTipo == rango.IdTipo &&
                    r.Anio == rango.Anio &&
                    r.IdOficina == rango.IdOficina &&
                    r.Activo &&
                    r.IdRango != rango.IdRango);

                if (cantidadActivosOficina >= maximoActivosPorOficina)
                    return OperacionResultado.Fail($"La oficina ya tiene {maximoActivosPorOficina} rango(s) activo(s) para este tipo y año.");
            }

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
            var rangos = await context.MaeNumeracionRangos
                .Where(r => r.IdTipo == idTipo && r.Anio == anio)
                .OrderBy(r => r.NumeroInicio)
                .ToListAsync();

            var detalleAjustes = new List<string>();

            if (rangos.Any())
            {
                var maximoUtilizado = rangos.Max(r => r.UltimoUtilizado);
                if (maximoUtilizado > cantidad)
                {
                    return OperacionResultado.Fail($"No se puede reducir el cupo a {cantidad} porque ya existen documentos numerados hasta {maximoUtilizado}. Ajuste el cupo a un valor mayor o igual al último número utilizado.");
                }

                foreach (var rango in rangos)
                {
                    if (rango.NumeroInicio > cantidad)
                    {
                        detalleAjustes.Add($"{rango.NombreRango} eliminado ({rango.NumeroInicio}-{rango.NumeroFin})");
                        context.MaeNumeracionRangos.Remove(rango);
                        continue;
                    }

                    if (rango.NumeroFin > cantidad)
                    {
                        detalleAjustes.Add($"{rango.NombreRango} ajustado: {rango.NumeroInicio}-{rango.NumeroFin} → {rango.NumeroInicio}-{cantidad}");
                        rango.NumeroFin = cantidad;
                    }
                }
            }

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
                Detalle = $"Se configuró cupo anual de {cantidad} para Tipo {idTipo} / Año {anio}" + (detalleAjustes.Count > 0 ? $". Ajustes automáticos: {string.Join("; ", detalleAjustes)}" : string.Empty),
                IdTipo = idTipo
            });

            await context.SaveChangesAsync();

            if (detalleAjustes.Count > 0)
            {
                return OperacionResultado.Ok($"Cupo guardado. Se ajustaron {detalleAjustes.Count} rango(s) para mantener consistencia: {string.Join("; ", detalleAjustes)}.");
            }

            return OperacionResultado.Ok("Cupo guardado correctamente.");
        }, "Error al guardar cupo", "guardar cupo");
    }

    public async Task<OperacionResultado> EliminarCupoAsync(int idTipo, int anio)
    {
        return await EjecutarOperacionControladaAsync(async () => {
            using var context = await _contextFactory.CreateDbContextAsync();
            var cupo = await context.MaeCuposSecretaria.FirstOrDefaultAsync(c => c.IdTipo == idTipo && c.Anio == anio);

            if (cupo == null)
            {
                return OperacionResultado.Fail("No existe el cupo seleccionado.");
            }

            var rangosAsociados = await context.MaeNumeracionRangos
                .Where(r => r.IdTipo == idTipo && r.Anio == anio)
                .CountAsync();

            if (rangosAsociados > 0)
            {
                return OperacionResultado.Fail($"No se puede eliminar el cupo porque hay {rangosAsociados} rango(s) asociados en oficinas. Elimine primero todos los rangos de ese tipo y año.");
            }

            context.MaeCuposSecretaria.Remove(cupo);
            context.MaeNumeracionBitacoras.Add(new MaeNumeracionBitacora
            {
                Fecha = DateTime.Now,
                Entidad = "CUPOS",
                Accion = "ELIMINACION",
                Detalle = $"Se eliminó el cupo anual Tipo {idTipo} / Año {anio}",
                IdTipo = idTipo
            });

            await context.SaveChangesAsync();
            return OperacionResultado.Ok("Cupo eliminado correctamente.");
        }, "Error al eliminar cupo", "eliminar cupo");
    }

    public async Task<SugerenciaRangoNumeracion> ObtenerSugerenciaRangoAsync(int idTipo, int anio, int? idOficina, int? idRangoActual, int? cantidadSolicitada)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var cupoTotal = await context.MaeCuposSecretaria.Where(c => c.IdTipo == idTipo && c.Anio == anio).Select(c => (int?)c.Cantidad).FirstOrDefaultAsync() ?? 0;
        var rangosExistentes = await context.MaeNumeracionRangos
            .Where(r => r.IdTipo == idTipo && r.Anio == anio && (!idRangoActual.HasValue || r.IdRango != idRangoActual.Value))
            .OrderBy(r => r.NumeroInicio).ToListAsync();

        var cantidadActivosOficina = idOficina.HasValue
            ? rangosExistentes.Count(r => r.IdOficina == idOficina.Value && r.Activo)
            : 0;
        var oficinaTieneActivos = cantidadActivosOficina > 0;
        var oficinaTieneActivosDisponibles = idOficina.HasValue && rangosExistentes.Any(r =>
            r.IdOficina == idOficina.Value &&
            r.Activo &&
            r.UltimoUtilizado < r.NumeroFin);

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
            SugerenciaRecortadaPorSaldo = (cantidad < (cantidadSolicitada ?? 50)),
            OficinaTieneRangoActivo = oficinaTieneActivos,
            RangoActivoAgotado = oficinaTieneActivos && !oficinaTieneActivosDisponibles,
            CantidadRangosActivosOficina = cantidadActivosOficina
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
    public static OperacionResultado Ok(string mensaje = "") => new() { Exitoso = true, Mensaje = mensaje };
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
    public int IdTipo { get; set; }
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
    public int CantidadRangosActivosOficina { get; set; }
}
