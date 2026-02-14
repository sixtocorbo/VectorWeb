using System.Data;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public class NumeracionRangoService
{
    private readonly SecretariaDbContext _context;

    public NumeracionRangoService(SecretariaDbContext context)
    {
        _context = context;
    }

    public async Task<List<MaeNumeracionRango>> ObtenerRangosAsync()
    {
        return await _context.MaeNumeracionRangos
            .Include(r => r.IdTipoNavigation)
            .Include(r => r.IdOficinaNavigation)
            .OrderByDescending(r => r.Anio)
            .ThenByDescending(r => r.Activo)
            .ThenBy(r => r.IdTipo)
            .ThenBy(r => r.IdOficina)
            .ToListAsync();
    }

    public async Task<List<MaeNumeracionBitacora>> ObtenerBitacoraAsync(int cantidad = 200)
    {
        var limite = cantidad <= 0 ? 200 : Math.Min(cantidad, 1000);

        return await _context.MaeNumeracionBitacoras
            .Include(b => b.IdTipoNavigation)
            .Include(b => b.IdOficinaNavigation)
            .Include(b => b.IdUsuarioNavigation)
            .OrderByDescending(b => b.Fecha)
            .ThenByDescending(b => b.IdBitacora)
            .Take(limite)
            .ToListAsync();
    }

    public async Task<List<CupoLibroMayorItem>> ObtenerLibroMayorCuposAsync()
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
    }

    public async Task GuardarRangoAsync(MaeNumeracionRango rango, int? idUsuario = null)
    {
        if (rango.IdTipo <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un tipo de documento válido.");
        }

        if (rango.NumeroInicio <= 0 || rango.NumeroFin <= 0 || rango.NumeroInicio > rango.NumeroFin)
        {
            throw new InvalidOperationException("El rango ingresado no es válido.");
        }

        var anioObjetivo = rango.Anio > 0 ? rango.Anio : DateTime.Now.Year;
        rango.Anio = anioObjetivo;

        if (rango.UltimoUtilizado < rango.NumeroInicio - 1)
        {
            rango.UltimoUtilizado = rango.NumeroInicio - 1;
        }

        if (rango.UltimoUtilizado > rango.NumeroFin)
        {
            throw new InvalidOperationException("El último utilizado no puede superar el número final.");
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
            throw new InvalidOperationException($"No hay cupo suficiente para este rango. Disponible: {Math.Max(0, disponible)} números para el año {anioObjetivo}.");
        }

        await ValidarUnicoActivoPorTipoOficinaAnioAsync(rango);

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
                throw new InvalidOperationException("El rango que intenta actualizar no existe.");
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
    }

    public async Task<int?> ObtenerCantidadCupoAsync(int idTipo, int anio)
    {
        if (idTipo <= 0 || anio <= 0)
        {
            return null;
        }

        return await _context.MaeCuposSecretaria
            .Where(c => c.IdTipo == idTipo && c.Anio == anio)
            .Select(c => (int?)c.Cantidad)
            .FirstOrDefaultAsync();
    }

    public async Task<int> ObtenerConsumoAcumuladoAsync(int idTipo, int anio, int? excluirIdRango = null)
    {
        if (idTipo <= 0 || anio <= 0)
        {
            return 0;
        }

        return await CalcularConsumoAcumuladoAsync(idTipo, anio, excluirIdRango, null);
    }

    public async Task EliminarRangoAsync(int idRango, int? idUsuario = null)
    {
        var rango = await _context.MaeNumeracionRangos
            .FirstOrDefaultAsync(r => r.IdRango == idRango);

        if (rango is null)
        {
            throw new InvalidOperationException("El rango que intenta eliminar no existe.");
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
    }

    public async Task GuardarCupoAsync(int idTipo, int anio, int cantidad, int? idUsuario = null)
    {
        if (idTipo <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un tipo de documento válido para configurar el cupo.");
        }

        if (anio <= 0)
        {
            throw new InvalidOperationException("Debe indicar un año válido para configurar el cupo.");
        }

        if (cantidad < 0)
        {
            throw new InvalidOperationException("El cupo no puede ser negativo.");
        }

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
    }

    public async Task<MaeNumeracionRango?> ObtenerRangoActivoAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        var anioObjetivo = anio ?? DateTime.Now.Year;
        return await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, anioObjetivo, incluirAgotados: false);
    }

    public async Task<bool> ExisteRangoConfiguradoAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        var anioObjetivo = anio ?? DateTime.Now.Year;
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
    }

    public async Task<MaeNumeracionRango> ConsumirSiguienteNumeroAsync(int idTipoDocumento, int? idOficina, int? anio = null)
    {
        var anioObjetivo = anio ?? DateTime.Now.Year;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var rango = await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, anioObjetivo, incluirAgotados: true);

        if (rango is null)
        {
            throw new InvalidOperationException("No hay un rango activo configurado para la oficina y tipo de documento seleccionados.");
        }

        if (rango.UltimoUtilizado >= rango.NumeroFin)
        {
            throw new InvalidOperationException($"Rango agotado ({rango.NombreRango}: {rango.NumeroInicio}-{rango.NumeroFin}). Debe registrar un nuevo rango para continuar.");
        }

        rango.UltimoUtilizado++;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return rango;
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

    private async Task ValidarUnicoActivoPorTipoOficinaAnioAsync(MaeNumeracionRango rango)
    {
        if (!rango.Activo)
        {
            return;
        }

        var existeActivo = await _context.MaeNumeracionRangos.AnyAsync(r =>
            r.IdRango != rango.IdRango &&
            r.IdTipo == rango.IdTipo &&
            r.Anio == rango.Anio &&
            r.Activo &&
            r.IdOficina == rango.IdOficina);

        if (existeActivo)
        {
            var oficinaTexto = rango.IdOficina.HasValue ? $"la oficina {rango.IdOficina}" : "el ámbito global";
            throw new InvalidOperationException($"Ya existe un rango activo para este tipo, año y {oficinaTexto}.");
        }
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

public sealed class CupoLibroMayorItem
{
    public string Tipo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public int Cantidad { get; set; }
    public int Consumido { get; set; }
    public int Disponible { get; set; }
    public DateTime Fecha { get; set; }
}
