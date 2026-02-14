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
            .OrderByDescending(r => r.Activo)
            .ThenBy(r => r.IdTipo)
            .ThenBy(r => r.NombreRango)
            .ToListAsync();
    }

    public async Task GuardarRangoAsync(MaeNumeracionRango rango)
    {
        if (rango.IdRango == 0)
        {
            if (rango.UltimoUtilizado < rango.NumeroInicio - 1)
            {
                rango.UltimoUtilizado = rango.NumeroInicio - 1;
            }

            rango.FechaCreacion ??= DateTime.Now;
            _context.MaeNumeracionRangos.Add(rango);
        }
        else
        {
            var rangoExistente = await _context.MaeNumeracionRangos
                .FirstOrDefaultAsync(r => r.IdRango == rango.IdRango);

            if (rangoExistente is null)
            {
                throw new InvalidOperationException("El rango que intenta actualizar no existe.");
            }

            _context.Entry(rangoExistente).CurrentValues.SetValues(rango);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<MaeNumeracionRango?> ObtenerRangoActivoAsync(int idTipoDocumento, int? idOficina)
    {
        return await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, incluirAgotados: false);
    }

    public async Task<MaeNumeracionRango> ConsumirSiguienteNumeroAsync(int idTipoDocumento, int? idOficina)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var rango = await ObtenerRangoAdministradoAsync(idTipoDocumento, idOficina, incluirAgotados: true);

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

    private async Task<MaeNumeracionRango?> ObtenerRangoAdministradoAsync(int idTipoDocumento, int? idOficina, bool incluirAgotados)
    {
        var baseQuery = _context.MaeNumeracionRangos
            .Where(r => r.IdTipo == idTipoDocumento && r.Activo);

        if (!incluirAgotados)
        {
            baseQuery = baseQuery.Where(r => r.UltimoUtilizado < r.NumeroFin);
        }

        if (idOficina.HasValue)
        {
            var rangoOficina = await baseQuery
                .Where(r => r.IdOficina == idOficina)
                .OrderBy(r => r.IdRango)
                .FirstOrDefaultAsync();

            if (rangoOficina is not null)
            {
                return rangoOficina;
            }
        }

        return await baseQuery
            .Where(r => r.IdOficina == null)
            .OrderBy(r => r.IdRango)
            .FirstOrDefaultAsync();
    }
}
