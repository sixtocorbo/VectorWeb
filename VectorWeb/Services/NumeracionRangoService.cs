using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VectorWeb.Models;

namespace VectorWeb.Services;

public class NumeracionRangoService
{
    private readonly SecretariaDbContext _context;
    private readonly ILogger<NumeracionRangoService> _logger;

    public NumeracionRangoService(SecretariaDbContext context, ILogger<NumeracionRangoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<MaeNumeracionRango>> ObtenerRangosAsync()
    {
        return await EjecutarLecturaSeguraAsync(async () =>
            await _context.MaeNumeracionRangos
                .Include(r => r.IdTipoNavigation)
                .Include(r => r.IdOficinaNavigation)
                .OrderByDescending(r => r.Anio)
                .ThenByDescending(r => r.Activo)
                .ThenBy(r => r.IdTipo)
                .ThenBy(r => r.IdOficina)
                .ToListAsync(),
            fallback: new List<MaeNumeracionRango>(),
            operacion: "obtener rangos de numeración");
    }

    public async Task<List<MaeNumeracionBitacora>> ObtenerBitacoraAsync(int cantidad = 200)
    {
        var limite = cantidad <= 0 ? 200 : Math.Min(cantidad, 1000);

        return await EjecutarLecturaSeguraAsync(async () =>
            await _context.MaeNumeracionBitacoras
                .Include(b => b.IdTipoNavigation)
                .Include(b => b.IdOficinaNavigation)
                .Include(b => b.IdUsuarioNavigation)
                .OrderByDescending(b => b.Fecha)
                .ThenByDescending(b => b.IdBitacora)
                .Take(limite)
                .ToListAsync(),
            fallback: new List<MaeNumeracionBitacora>(),
            operacion: "obtener bitácora de numeración");
    }

    public async Task<List<CupoLibroMayorItem>> ObtenerLibroMayorCuposAsync()
    {
        return await EjecutarLecturaSeguraAsync(async () =>
        {
            var cupos = await _context.MaeCuposSecretaria
                .AsNoTracking()
                .Include(c => c.IdTipoNavigation)
                .OrderByDescending(c => c.Anio)
                .ThenBy(c => c.IdTipoNavigation.Nombre)
                .ToListAsync();

            if (cupos.Count == 0)
            {
                return new List<CupoLibroMayorItem>();
            }

            var consumoPorTipoAnio = await _context.MaeNumeracionRangos
                .AsNoTracking()
                .GroupBy(r => new { r.IdTipo, r.Anio })
                .Select(g => new
                {
                    g.Key.IdTipo,
                    g.Key.Anio,
                    Consumo = g.Sum(x => x.NumeroFin - x.NumeroInicio + 1)
                })
                .ToListAsync();

            var consumoLookup = consumoPorTipoAnio.ToDictionary(x => (x.IdTipo, x.Anio), x => x.Consumo);

            return cupos
                .Select(c =>
                {
                    consumoLookup.TryGetValue((c.IdTipo, c.Anio), out var consumido);
                    return new CupoLibroMayorItem
                    {
                        Tipo = c.IdTipoNavigation?.Nombre ?? $"Tipo {c.IdTipo}",
                        Anio = c.Anio,
                        Cantidad = c.Cantidad,
                        Consumido = consumido,
                        Disponible = Math.Max(0, c.Cantidad - consumido),
                        Fecha = c.Fecha
                    };
                })
                .ToList();
        },
            fallback: new List<CupoLibroMayorItem>(),
            operacion: "obtener libro mayor de cupos");
    }

    public async Task<OperacionResultado> GuardarRangoAsync(MaeNumeracionRango rango, int? idUsuario = null)
    {
        if (rango.IdTipo <= 0)
        {
            return OperacionResultado.Fail("Debe seleccionar un tipo de documento válido.");
        }

        if (rango.NumeroInicio <= 0 || rango.NumeroFin <= 0 || rango.NumeroInicio > rango.NumeroFin)
        {
            return OperacionResultado.Fail("El rango ingresado no es válido.");
        }

        if (rango.UltimoUtilizado > rango.NumeroFin)
        {
            return OperacionResultado.Fail("El último utilizado no puede superar el número final.");
        }

        return await EjecutarOperacionControladaAsync(async () =>
        {
            var anioObjetivo = rango.Anio > 0 ? rango.Anio : DateTime.Now.Year;
            rango.Anio = anioObjetivo;

            if (rango.UltimoUtilizado < rango.NumeroInicio - 1)
            {
                rango.UltimoUtilizado = rango.NumeroInicio - 1;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var nombreGenerado = await GenerarNombreRangoAsync(rango.IdTipo, rango.IdOficina, anioObjetivo, rango.NumeroInicio, rango.NumeroFin);
            rango.NombreRango = nombreGenerado;

            await AsegurarCupoSecretariaAsync(rango.IdTipo, anioObjetivo);

            var consumoSolicitado = rango.NumeroFin - rango.NumeroInicio + 1;
            var consumoAcumulado = await CalcularConsumoAcumuladoAsync(rango.IdTipo, anioObjetivo, rango.IdRango == 0 ? null : rango.IdRango, rango.IdOficina);
            var cupo = await _context.MaeCuposSecretaria
                .FirstAsync(c => c.IdTipo == rango.IdTipo && c.Anio == anioObjetivo);

            if (consumoAcumulado + consumoSolicitado > cupo.Cantidad)
            {
                var disponible = cupo.Cantidad - consumoAcumulado;
                return OperacionResultado.Fail($"No hay cupo suficiente para este rango. Disponible: {Math.Max(0, disponible)} números para el año {anioObjetivo}.");
            }

            await DesactivarRangosAgotadosAsync(rango);
            await DesactivarRangosActivosEnConflictoAsync(rango);

            var mensajeConflictoActivo = await ObtenerMensajeUnicoActivoPorTipoOficinaAnioAsync(rango);
            if (!string.IsNullOrWhiteSpace(mensajeConflictoActivo))
            {
                return OperacionResultado.Fail(mensajeConflictoActivo);
            }

            if (rango.IdRango == 0)
            {
                rango.FechaCreacion ??= DateTime.Now;
                _context.MaeNumeracionRangos.Add(rango);

                await RegistrarBitacoraAsync(
                    entidad: "RANGO",
                    accion: "APERTURA",
                    detalle: $"Apertura de rango {rango.NombreRango} ({rango.NumeroInicio}-{rango.NumeroFin}).",
                    idTipo: rango.IdTipo,
                    anio: rango.Anio,
                    idOficina: rango.IdOficina,
                    idUsuario: idUsuario,
                    idReferencia: rango.IdRango == 0 ? null : rango.IdRango);
            }
            else
            {
                var rangoExistente = await _context.MaeNumeracionRangos
                    .FirstOrDefaultAsync(r => r.IdRango == rango.IdRango);

                if (rangoExistente is null)
                {
                    return OperacionResultado.Fail("El rango que intenta actualizar no existe.");
                }

                var accion = DeterminarAccionRango(rangoExistente, rango);
                var detalle = ConstruirDetalleCambioRango(rangoExistente, rango);

                _context.Entry(rangoExistente).CurrentValues.SetValues(rango);

                await RegistrarBitacoraAsync(
                    entidad: "RANGO",
                    accion: accion,
                    detalle: detalle,
                    idTipo: rango.IdTipo,
                    anio: rango.Anio,
                    idOficina: rango.IdOficina,
                    idUsuario: idUsuario,
                    idReferencia: rango.IdRango);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return OperacionResultado.Ok();
        }, "No fue posible guardar el rango en este momento.", "guardar rango de numeración");
    }

    private async Task DesactivarRangosAgotadosAsync(MaeNumeracionRango rango)
    {
        if (!rango.Activo)
        {
            return;
        }

        var rangosAgotadosActivos = await _context.MaeNumeracionRangos
            .Where(r =>
                r.IdRango != rango.IdRango &&
                r.IdTipo == rango.IdTipo &&
                r.Anio == rango.Anio &&
                r.Activo &&
                r.IdOficina == rango.IdOficina &&
                r.UltimoUtilizado >= r.NumeroFin)
            .ToListAsync();

        if (rangosAgotadosActivos.Count == 0)
        {
            return;
        }

        foreach (var rangoAgotado in rangosAgotadosActivos)
        {
            rangoAgotado.Activo = false;
        }
    }

    private async Task DesactivarRangosActivosEnConflictoAsync(MaeNumeracionRango rango)
    {
        if (!rango.Activo)
        {
            return;
        }

        var rangosActivosEnConflicto = await _context.MaeNumeracionRangos
            .Where(r =>
                r.IdRango != rango.IdRango &&
                r.IdTipo == rango.IdTipo &&
                r.Anio == rango.Anio &&
                r.Activo &&
                r.IdOficina == rango.IdOficina)
            .ToListAsync();

        if (rangosActivosEnConflicto.Count == 0)
        {
            return;
        }

        foreach (var rangoActivo in rangosActivosEnConflicto)
        {
            rangoActivo.Activo = false;
        }
    }

    public async Task<int?> ObtenerCantidadCupoAsync(int idTipo, int anio)
    {
        if (idTipo <= 0 || anio <= 0)
        {
            return null;
        }

        return await EjecutarLecturaSeguraAsync(async () =>
            await _context.MaeCuposSecretaria
                .Where(c => c.IdTipo == idTipo && c.Anio == anio)
                .Select(c => (int?)c.Cantidad)
                .FirstOrDefaultAsync(),
            fallback: null,
            operacion: "obtener cantidad de cupo");
    }

    public async Task<int> ObtenerConsumoAcumuladoAsync(int idTipo, int anio, int? excluirIdRango = null)
    {
        if (idTipo <= 0 || anio <= 0)
        {
            return 0;
        }

        return await EjecutarLecturaSeguraAsync(async () =>
            await CalcularConsumoAcumuladoAsync(idTipo, anio, excluirIdRango, null),
            fallback: 0,
            operacion: "obtener consumo acumulado");
    }

    public async Task<OperacionResultado> EliminarRangoAsync(int idRango, int? idUsuario = null)
    {
        return await EjecutarOperacionControladaAsync(async () =>
        {
            var rango = await _context.MaeNumeracionRangos
                .FirstOrDefaultAsync(r => r.IdRango == idRango);

            if (rango is null)
            {
                return OperacionResultado.Fail("El rango que intenta eliminar no existe.");
            }

            _context.MaeNumeracionRangos.Remove(rango);

            await RegistrarBitacoraAsync(
                entidad: "RANGO",
                accion: "ELIMINACION",
                detalle: $"Eliminación de rango {rango.NombreRango} ({rango.NumeroInicio}-{rango.NumeroFin}).",
                idTipo: rango.IdTipo,
                anio: rango.Anio,
                idOficina: rango.IdOficina,
                idUsuario: idUsuario,
                idReferencia: rango.IdRango);

            await _context.SaveChangesAsync();
            return OperacionResultado.Ok();
        }, "No fue posible eliminar el rango en este momento.", "eliminar rango de numeración");
    }

    public async Task<OperacionResultado> GuardarCupoAsync(int idTipo, int anio, int cantidad, int? idUsuario = null)
    {
        if (idTipo <= 0)
        {
            return OperacionResultado.Fail("Debe seleccionar un tipo de documento válido para configurar el cupo.");
        }

        if (anio <= 0)
        {
            return OperacionResultado.Fail("Debe indicar un año válido para configurar el cupo.");
        }

        if (cantidad < 0)
        {
            return OperacionResultado.Fail("El cupo no puede ser negativo.");
        }

        return await EjecutarOperacionControladaAsync(async () =>
        {
            var cupoExistente = await _context.MaeCuposSecretaria
                .FirstOrDefaultAsync(c => c.IdTipo == idTipo && c.Anio == anio);

            if (cupoExistente is null)
            {
                var tipo = await _context.CatTipoDocumentos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.IdTipo == idTipo);

                var codigoTipo = tipo?.Codigo?.Trim();
                if (string.IsNullOrWhiteSpace(codigoTipo))
                {
                    codigoTipo = $"TIPO{idTipo}";
                }

                cupoExistente = new MaeCuposSecretarium
                {
                    IdTipo = idTipo,
                    Anio = anio,
                    Fecha = DateTime.Now,
                    Cantidad = cantidad,
                    NombreCupo = $"CUPO-{codigoTipo}-{anio}"
                };

                _context.MaeCuposSecretaria.Add(cupoExistente);

                await RegistrarBitacoraAsync(
                    entidad: "CUPO",
                    accion: "APERTURA",
                    detalle: $"Creación de cupo anual con cantidad {cantidad}.",
                    idTipo: idTipo,
                    anio: anio,
                    idOficina: null,
                    idUsuario: idUsuario,
                    idReferencia: null);
            }
            else
            {
                var cantidadAnterior = cupoExistente.Cantidad;
                cupoExistente.Cantidad = cantidad;
                cupoExistente.Fecha = DateTime.Now;

                await RegistrarBitacoraAsync(
                    entidad: "CUPO",
                    accion: "CAMBIO",
                    detalle: $"Actualización de cupo anual de {cantidadAnterior} a {cantidad}.",
                    idTipo: idTipo,
                    anio: anio,
                    idOficina: null,
                    idUsuario: idUsuario,
                    idReferencia: cupoExistente.IdCupo);
            }

            await _context.SaveChangesAsync();
            return OperacionResultado.Ok();
        }, "No fue posible guardar el cupo en este momento.", "guardar cupo de numeración");
    }

    public async Task<MaeNumeracionRango?> ObtenerRangoActivoAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        var anioObjetivo = anio ?? DateTime.Now.Year;
        return await EjecutarLecturaSeguraAsync(async () =>
            await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, anioObjetivo, incluirAgotados: false),
            fallback: null,
            operacion: "obtener rango activo");
    }

    public async Task<bool> ExisteRangoConfiguradoAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        var anioObjetivo = anio ?? DateTime.Now.Year;
        return await EjecutarLecturaSeguraAsync(async () =>
        {
            var baseQuery = _context.MaeNumeracionRangos
                .Where(r => r.IdTipo == idTipoDocumento && r.Activo && r.Anio == anioObjetivo);

        if (idOficina.HasValue)
        {
            var existeRangoOficina = await baseQuery.AnyAsync(r => r.IdOficina == idOficina);
            if (existeRangoOficina)
            {
                return true;
            }
        }

            return await baseQuery.AnyAsync(r => r.IdOficina == null);
        }, fallback: false, operacion: "verificar existencia de rango configurado");
    }

    public async Task<OperacionResultado<MaeNumeracionRango>> ConsumirSiguienteNumeroAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        return await EjecutarOperacionControladaAsync(async () =>
        {
            var anioObjetivo = anio ?? DateTime.Now.Year;
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var rango = await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, anioObjetivo, incluirAgotados: true);

            if (rango is null)
            {
                return OperacionResultado<MaeNumeracionRango>.Fail("No hay un rango activo configurado para la oficina y tipo de documento seleccionados.");
            }

            if (rango.UltimoUtilizado >= rango.NumeroFin)
            {
                return OperacionResultado<MaeNumeracionRango>.Fail($"Rango agotado ({rango.NombreRango}: {rango.NumeroInicio}-{rango.NumeroFin}). Debe registrar un nuevo rango para continuar.");
            }

            rango.UltimoUtilizado++;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return OperacionResultado<MaeNumeracionRango>.Ok(rango);
        }, "No fue posible consumir el siguiente número oficial.", "consumir siguiente número");
    }

    private async Task<T> EjecutarLecturaSeguraAsync<T>(Func<Task<T>> accion, T fallback, string operacion)
    {
        try
        {
            return await accion();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al {Operacion}.", operacion);
            return fallback;
        }
    }

    private async Task<OperacionResultado> EjecutarOperacionControladaAsync(Func<Task<OperacionResultado>> accion, string mensajeUsuario, string operacion)
    {
        try
        {
            return await accion();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Conflicto de concurrencia al {Operacion}.", operacion);
            return OperacionResultado.Fail("Otro usuario modificó la información al mismo tiempo. Recargue la vista e intente nuevamente.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error de base de datos al {Operacion}.", operacion);
            return OperacionResultado.Fail(mensajeUsuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al {Operacion}.", operacion);
            return OperacionResultado.Fail(mensajeUsuario);
        }
    }

    private async Task<OperacionResultado<T>> EjecutarOperacionControladaAsync<T>(Func<Task<OperacionResultado<T>>> accion, string mensajeUsuario, string operacion)
    {
        try
        {
            return await accion();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Conflicto de concurrencia al {Operacion}.", operacion);
            return OperacionResultado<T>.Fail("Otro usuario modificó la información al mismo tiempo. Recargue la vista e intente nuevamente.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error de base de datos al {Operacion}.", operacion);
            return OperacionResultado<T>.Fail(mensajeUsuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al {Operacion}.", operacion);
            return OperacionResultado<T>.Fail(mensajeUsuario);
        }
    }

    private async Task<MaeNumeracionRango?> ObtenerRangoAdministradoAsync(int idTipoDocumento, int? idOficina, int anio, bool incluirAgotados)
    {
        var baseQuery = _context.MaeNumeracionRangos
            .Where(r => r.IdTipo == idTipoDocumento && r.Activo && r.Anio == anio);

        if (!incluirAgotados)
        {
            baseQuery = baseQuery.Where(r => r.UltimoUtilizado < r.NumeroFin);
        }

        if (idOficina.HasValue)
        {
            var rangoOficina = await baseQuery
                .Where(r => r.IdOficina == idOficina)
                .OrderBy(r => r.NumeroInicio)
                .ThenBy(r => r.IdRango)
                .FirstOrDefaultAsync();

            if (rangoOficina is not null)
            {
                return rangoOficina;
            }
        }

        return await baseQuery
            .Where(r => r.IdOficina == null)
            .OrderBy(r => r.NumeroInicio)
            .ThenBy(r => r.IdRango)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> ObtenerMensajeUnicoActivoPorTipoOficinaAnioAsync(MaeNumeracionRango rango)
    {
        if (!rango.Activo)
        {
            return null;
        }

        var existeActivo = await _context.MaeNumeracionRangos.AnyAsync(r =>
            r.IdRango != rango.IdRango &&
            r.IdTipo == rango.IdTipo &&
            r.Anio == rango.Anio &&
            r.Activo &&
            r.IdOficina == rango.IdOficina);

        if (existeActivo)
        {
            var oficinaTexto = "el ámbito global";

            if (rango.IdOficina.HasValue)
            {
                var nombreOficina = await _context.CatOficinas
                    .AsNoTracking()
                    .Where(o => o.IdOficina == rango.IdOficina.Value)
                    .Select(o => o.Nombre)
                    .FirstOrDefaultAsync();

                var etiquetaOficina = string.IsNullOrWhiteSpace(nombreOficina)
                    ? rango.IdOficina.Value.ToString()
                    : nombreOficina.Trim();

                oficinaTexto = $"la oficina {etiquetaOficina}";
            }

            return $"Ya existe un rango activo para este tipo, año y {oficinaTexto}.";
        }

        return null;
    }

    private async Task<int> CalcularConsumoAcumuladoAsync(int idTipo, int anio, int? excluirIdRango, int? idOficina)
    {
        var query = _context.MaeNumeracionRangos
            .Where(r => r.IdTipo == idTipo && r.Anio == anio && (!excluirIdRango.HasValue || r.IdRango != excluirIdRango.Value));

        if (idOficina.HasValue)
        {
            query = query.Where(r => r.IdOficina == idOficina.Value);
        }

        return await query
            .SumAsync(r => r.NumeroFin - r.NumeroInicio + 1);
    }

    private async Task AsegurarCupoSecretariaAsync(int idTipo, int anio)
    {
        var cupoExistente = await _context.MaeCuposSecretaria
            .FirstOrDefaultAsync(c => c.IdTipo == idTipo && c.Anio == anio);

        if (cupoExistente is not null)
        {
            return;
        }

        var tipo = await _context.CatTipoDocumentos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTipo == idTipo);

        var codigoTipo = tipo?.Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigoTipo))
        {
            codigoTipo = $"TIPO{idTipo}";
        }

        _context.MaeCuposSecretaria.Add(new MaeCuposSecretarium
        {
            IdTipo = idTipo,
            Anio = anio,
            Fecha = DateTime.Now,
            Cantidad = 0,
            NombreCupo = $"CUPO-{codigoTipo}-{anio}"
        });

        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerarNombreRangoAsync(int idTipo, int? idOficina, int anio, int numeroInicio, int numeroFin)
    {
        var tipo = await _context.CatTipoDocumentos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTipo == idTipo);

        var codigoTipo = tipo?.Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigoTipo))
        {
            codigoTipo = $"TIPO{idTipo}";
        }

        var segmentoOficina = "GLOBAL";
        if (idOficina.HasValue)
        {
            var oficina = await _context.CatOficinas
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdOficina == idOficina.Value);

            segmentoOficina = NormalizarToken(oficina?.Nombre, $"OFI{idOficina.Value}");
        }

        return $"{codigoTipo}-{segmentoOficina}-{anio}-{numeroInicio:D6}-{numeroFin:D6}";
    }

    private static string NormalizarToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var upper = value.Trim().ToUpperInvariant();
        var normalizado = new string(upper
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (normalizado.Contains("--"))
        {
            normalizado = normalizado.Replace("--", "-");
        }

        normalizado = normalizado.Trim('-');
        return string.IsNullOrWhiteSpace(normalizado) ? fallback : normalizado;
    }

    private async Task RegistrarBitacoraAsync(
        string entidad,
        string accion,
        string detalle,
        int idTipo,
        int anio,
        int? idOficina,
        int? idUsuario,
        int? idReferencia)
    {
        _context.MaeNumeracionBitacoras.Add(new MaeNumeracionBitacora
        {
            Fecha = DateTime.Now,
            Entidad = entidad,
            Accion = accion,
            Detalle = detalle,
            IdTipo = idTipo,
            Anio = anio,
            IdOficina = idOficina,
            IdUsuario = idUsuario,
            IdReferencia = idReferencia
        });

        await Task.CompletedTask;
    }

    private static string DeterminarAccionRango(MaeNumeracionRango anterior, MaeNumeracionRango actualizado)
    {
        if (anterior.Activo && !actualizado.Activo)
        {
            return "CIERRE";
        }

        if (!anterior.Activo && actualizado.Activo)
        {
            return "REAPERTURA";
        }

        return "CAMBIO";
    }

    private static string ConstruirDetalleCambioRango(MaeNumeracionRango anterior, MaeNumeracionRango actualizado)
    {
        var cambios = new List<string>();

        if (anterior.NumeroInicio != actualizado.NumeroInicio || anterior.NumeroFin != actualizado.NumeroFin)
        {
            cambios.Add($"intervalo {anterior.NumeroInicio}-{anterior.NumeroFin} -> {actualizado.NumeroInicio}-{actualizado.NumeroFin}");
        }

        if (anterior.UltimoUtilizado != actualizado.UltimoUtilizado)
        {
            cambios.Add($"último utilizado {anterior.UltimoUtilizado} -> {actualizado.UltimoUtilizado}");
        }

        if (anterior.IdOficina != actualizado.IdOficina)
        {
            cambios.Add($"oficina {(anterior.IdOficina?.ToString() ?? "GLOBAL")} -> {(actualizado.IdOficina?.ToString() ?? "GLOBAL")}");
        }

        if (anterior.Anio != actualizado.Anio)
        {
            cambios.Add($"año {anterior.Anio} -> {actualizado.Anio}");
        }

        if (anterior.Activo != actualizado.Activo)
        {
            cambios.Add($"estado {(anterior.Activo ? "activo" : "inactivo")} -> {(actualizado.Activo ? "activo" : "inactivo")}");
        }

        if (cambios.Count == 0)
        {
            return $"Actualización sin cambios de campos de control en el rango {actualizado.NombreRango}.";
        }

        return $"Actualización de rango {actualizado.NombreRango}: {string.Join(", ", cambios)}.";
    }
}


public sealed class OperacionResultado
{
    public bool Exitoso { get; private set; }
    public string Mensaje { get; private set; } = string.Empty;

    public static OperacionResultado Ok() => new() { Exitoso = true };

    public static OperacionResultado Fail(string mensaje) => new()
    {
        Exitoso = false,
        Mensaje = string.IsNullOrWhiteSpace(mensaje)
            ? "No fue posible completar la operación."
            : mensaje
    };
}

public sealed class OperacionResultado<T>
{
    public bool Exitoso { get; private set; }
    public string Mensaje { get; private set; } = string.Empty;
    public T? Data { get; private set; }

    public static OperacionResultado<T> Ok(T data) => new()
    {
        Exitoso = true,
        Data = data
    };

    public static OperacionResultado<T> Fail(string mensaje) => new()
    {
        Exitoso = false,
        Mensaje = string.IsNullOrWhiteSpace(mensaje)
            ? "No fue posible completar la operación."
            : mensaje
    };
}

public sealed class CupoLibroMayorItem
{
    public string Tipo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public int Cantidad { get; set; }
    public int Consumido { get; set; }
    public int Disponible { get; set; }
    public DateTime Fecha { get; set; }
}
