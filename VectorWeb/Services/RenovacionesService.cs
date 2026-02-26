using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VectorWeb.Models;
using VectorWeb.Repositories;

namespace VectorWeb.Services;

// --- DTOs y Enums (Compatibilidad con UI) ---
public sealed class SalidaGridDto
{
    public int IdSalida { get; set; }
    public int IdRecluso { get; set; }
    public string Recluso { get; set; } = string.Empty;
    public string LugarTrabajo { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int DiasRestantes { get; set; }
    public EstadoRenovacion Estado { get; set; } = EstadoRenovacion.Ok;
    public bool Activo { get; set; }
    public int CantidadDocumentos { get; set; }
    public string? DetalleCustodia { get; set; }
    public string? Autorizacion { get; set; }
}

public enum EstadoRenovacion { Ok, Alerta, Vencida, Inactiva }

public sealed class ObservacionesRenovacionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
}

public sealed class DocumentoRespaldoDto
{
    public long IdDocumento { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public string? Descripcion { get; set; }
    public string? NumeroOficial { get; set; }
    public DateTime? FechaCreacion { get; set; }
}

// --- SERVICIO BLINDADO ---
public sealed class RenovacionesService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly IRepository<CfgSistemaParametro> _repoParametros;
    private const string ClaveDiasAlertaRenovaciones = "RENOVACIONES_DIAS_ALERTA";
    private const int DiasAlertaDefecto = 30;
    private const string CollationBusquedaSinAcentos = "Modern_Spanish_CI_AI";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public RenovacionesService(IDbContextFactory<SecretariaDbContext> contextFactory, IRepository<CfgSistemaParametro> repoParametros)
    {
        _contextFactory = contextFactory;
        _repoParametros = repoParametros;
    }

    public async Task<List<SalidaGridDto>> ObtenerSalidasAsync(bool soloActivas, string textoBuscar)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var hoy = DateTime.Today;
        var diasAlerta = await ObtenerDiasAlertaRenovacionesAsync();

        var query = context.TraSalidasLaborales
            .AsNoTracking()
            .Include(s => s.IdReclusoNavigation)
            .Include(s => s.TraSalidasLaboralesDocumentoRespaldos)
            .AsQueryable();

        query = soloActivas ? query.Where(s => s.Activo == true) : query.Where(s => s.Activo != true);

        if (!string.IsNullOrWhiteSpace(textoBuscar))
        {
            var terminos = textoBuscar.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var termino in terminos)
            {
                var patron = $"%{termino}%";
                query = query.Where(s =>
                    (s.IdReclusoNavigation.NombreCompleto != null && EF.Functions.Like(EF.Functions.Collate(s.IdReclusoNavigation.NombreCompleto, CollationBusquedaSinAcentos), patron)) ||
                    (s.LugarTrabajo != null && EF.Functions.Like(EF.Functions.Collate(s.LugarTrabajo, CollationBusquedaSinAcentos), patron)) ||
                    (s.Observaciones != null && EF.Functions.Like(EF.Functions.Collate(s.Observaciones, CollationBusquedaSinAcentos), patron)));
            }
        }

        var datos = await query
            .OrderBy(s => s.FechaVencimiento)
            .Select(s => new {
                Salida = s,
                NombreRecluso = s.IdReclusoNavigation.NombreCompleto,
                CantDocs = s.TraSalidasLaboralesDocumentoRespaldos.Count
            })
            .ToListAsync();

        return datos.Select(x => {
            var dias = (x.Salida.FechaVencimiento.Date - hoy).Days;
            var activa = x.Salida.Activo ?? true;
            var obsDto = ParsearObservaciones(x.Salida.Observaciones);

            return new SalidaGridDto
            {
                IdSalida = x.Salida.IdSalida,
                IdRecluso = x.Salida.IdRecluso,
                Recluso = x.NombreRecluso,
                LugarTrabajo = x.Salida.LugarTrabajo,
                FechaInicio = x.Salida.FechaInicio,
                FechaVencimiento = x.Salida.FechaVencimiento,
                DiasRestantes = dias,
                Estado = CalcularEstado(activa, dias, diasAlerta),
                Activo = activa,
                CantidadDocumentos = x.CantDocs,
                DetalleCustodia = x.Salida.DetalleCustodia,
                Autorizacion = obsDto.Codigo
            };
        }).ToList();
    }

    public async Task<TraSalidasLaborale?> ObtenerPorIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TraSalidasLaborales
            .Include(s => s.IdReclusoNavigation)
            .Include(s => s.TraSalidasLaboralesDocumentoRespaldos)
            .FirstOrDefaultAsync(s => s.IdSalida == id);
    }

    public async Task GuardarAsync(TraSalidasLaborale entidad, List<long> idsDocumentos, string cod, string desc, string obs)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        entidad.Observaciones = ConstruirObservaciones(cod, desc, obs);

        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            if (entidad.IdSalida == 0) context.TraSalidasLaborales.Add(entidad);
            else context.TraSalidasLaborales.Update(entidad);

            await context.SaveChangesAsync();

            if (idsDocumentos != null)
            {
                var idsNormalizados = idsDocumentos.Where(x => x > 0).Distinct().ToList();
                await ActualizarVinculosInternalAsync(context, entidad.IdSalida, idsNormalizados);
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private async Task ActualizarVinculosInternalAsync(SecretariaDbContext context, int idSalida, List<long> nuevosIds)
    {
        var existentes = await context.TraSalidasLaboralesDocumentoRespaldos
            .Where(x => x.IdSalida == idSalida).ToListAsync();

        var aBorrar = existentes.Where(x => !nuevosIds.Contains(x.IdDocumento)).ToList();
        if (aBorrar.Count > 0) context.TraSalidasLaboralesDocumentoRespaldos.RemoveRange(aBorrar);

        var idsExistentes = existentes.Select(x => x.IdDocumento).ToHashSet();
        foreach (var idDoc in nuevosIds.Where(id => !idsExistentes.Contains(id)))
        {
            context.TraSalidasLaboralesDocumentoRespaldos.Add(new TraSalidasLaboralesDocumentoRespaldo
            {
                IdSalida = idSalida,
                IdDocumento = idDoc,
                FechaRegistro = DateTime.Now
            });
        }
        await context.SaveChangesAsync();
    }

    // --- CORRECCIÓN FINAL PARA ERROR CS1061 EN PÁGINA RENOVACIONES ---
    public async Task CambiarEstadoAsync(int idSalida, bool activo, string motivo)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entidad = await context.TraSalidasLaborales.FindAsync(idSalida);
        if (entidad is null) return;

        entidad.Activo = activo;

        if (!activo && !string.IsNullOrWhiteSpace(motivo))
        {
            var dto = ParsearObservaciones(entidad.Observaciones);
            dto.Observacion += $"{Environment.NewLine}[{DateTime.Now:g}] Motivo cese: {motivo}";
            entidad.Observaciones = JsonSerializer.Serialize(dto, _jsonOptions);
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<DocumentoRespaldoDto>> BuscarDocumentosCandidatos(int idRecluso, string filtroTexto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.MaeDocumentos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            query = query.Where(d => d.Asunto.Contains(filtroTexto) ||
                                     (d.NumeroOficial != null && d.NumeroOficial.Contains(filtroTexto)) ||
                                     d.IdDocumento.ToString() == filtroTexto);
        }

        return await query.OrderByDescending(d => d.FechaCreacion).Take(20)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Descripcion = d.Descripcion,
                NumeroOficial = d.NumeroOficial,
                FechaCreacion = d.FechaCreacion,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            }).ToListAsync();
    }

    public async Task<bool> ExisteDocumentoAsync(long idDocumento)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeDocumentos.AnyAsync(x => x.IdDocumento == idDocumento);
    }

    public async Task<DocumentoRespaldoDto?> ObtenerDocumentoPorIdAsync(long idDocumento)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MaeDocumentos.AsNoTracking()
            .Where(d => d.IdDocumento == idDocumento)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Descripcion = d.Descripcion,
                NumeroOficial = d.NumeroOficial,
                FechaCreacion = d.FechaCreacion,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            }).FirstOrDefaultAsync();
    }

    public async Task<int> ObtenerDiasAlertaRenovacionesAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var valor = await context.CfgSistemaParametros.AsNoTracking()
            .Where(p => p.Clave == ClaveDiasAlertaRenovaciones).Select(p => p.Valor).FirstOrDefaultAsync();

        return int.TryParse(valor, out var d) && d >= 0 ? d : DiasAlertaDefecto;
    }

    private static EstadoRenovacion CalcularEstado(bool activa, int dias, int alerta)
    {
        if (!activa) return EstadoRenovacion.Inactiva;
        if (dias < 0) return EstadoRenovacion.Vencida;
        return (dias <= alerta) ? EstadoRenovacion.Alerta : EstadoRenovacion.Ok;
    }

    public ObservacionesRenovacionDto ParsearObservaciones(string? obs)
    {
        if (string.IsNullOrWhiteSpace(obs)) return new ObservacionesRenovacionDto();
        try { return JsonSerializer.Deserialize<ObservacionesRenovacionDto>(obs, _jsonOptions) ?? new(); }
        catch { return new(); }
    }

    private static string ConstruirObservaciones(string c, string d, string o) =>
        JsonSerializer.Serialize(new ObservacionesRenovacionDto { Codigo = c, Descripcion = d, Observacion = o }, _jsonOptions);
}
