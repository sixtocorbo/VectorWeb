using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization; // Necesario para controlar nombres en JSON si se desea
using VectorWeb.Models;
using VectorWeb.Repositories;

namespace VectorWeb.Services;

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

public enum EstadoRenovacion
{
    Ok,
    Alerta,
    Vencida,
    Inactiva
}

// CORRECCIÓN: Renombradas las propiedades para coincidir con lo que espera EditarRenovacion.razor
public sealed class ObservacionesRenovacionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    // Mapeamos "Observacion" (Razor) a "ObservacionesUsuario" (Lógica interna) si prefieres, 
    // o simplemente usamos "Observacion" para mantener compatibilidad total.
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

public sealed class RenovacionesService
{
    private readonly SecretariaDbContext context;
    private readonly IRepository<CfgSistemaParametro> _repoParametros;
    private const string ClaveDiasAlertaRenovaciones = "RENOVACIONES_DIAS_ALERTA";
    private const int DiasAlertaDefecto = 30;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public RenovacionesService(SecretariaDbContext context, IRepository<CfgSistemaParametro> repoParametros)
    {
        this.context = context;
        _repoParametros = repoParametros;
    }

    public async Task<List<SalidaGridDto>> ObtenerSalidasAsync(bool soloActivas, string textoBuscar)
    {
        var hoy = DateTime.Today;
        var diasAlerta = await ObtenerDiasAlertaRenovacionesAsync();

        var query = context.TraSalidasLaborales
            .AsNoTracking()
            .Include(s => s.IdReclusoNavigation)
            .Include(s => s.TraSalidasLaboralesDocumentoRespaldos)
            .AsQueryable();

        query = soloActivas
            ? query.Where(s => s.Activo == true)
            : query.Where(s => s.Activo != true);

        if (!string.IsNullOrWhiteSpace(textoBuscar))
        {
            var terminos = textoBuscar
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var termino in terminos)
            {
                var filtro = termino;
                query = query.Where(s =>
                    s.IdReclusoNavigation.NombreCompleto.ToUpper().Contains(filtro) ||
                    s.LugarTrabajo.ToUpper().Contains(filtro) ||
                    (s.Observaciones != null && s.Observaciones.ToUpper().Contains(filtro)));
            }
        }

        var datos = await query
            .OrderBy(s => s.FechaVencimiento)
            .Select(s => new
            {
                Salida = s,
                NombreRecluso = s.IdReclusoNavigation.NombreCompleto,
                CantDocs = s.TraSalidasLaboralesDocumentoRespaldos.Count
            })
            .ToListAsync();

        return datos.Select(x =>
        {
            var dias = (x.Salida.FechaVencimiento.Date - hoy).Days;
            var activa = x.Salida.Activo ?? true;
            var estado = CalcularEstado(activa, dias, diasAlerta);

            // Ahora devuelve el objeto DTO con las propiedades correctas
            var autorizacionDto = ParsearObservaciones(x.Salida.Observaciones);

            return new SalidaGridDto
            {
                IdSalida = x.Salida.IdSalida,
                IdRecluso = x.Salida.IdRecluso,
                Recluso = x.NombreRecluso,
                LugarTrabajo = x.Salida.LugarTrabajo,
                FechaInicio = x.Salida.FechaInicio,
                FechaVencimiento = x.Salida.FechaVencimiento,
                DiasRestantes = dias,
                Estado = estado,
                Activo = activa,
                CantidadDocumentos = x.CantDocs,
                DetalleCustodia = x.Salida.DetalleCustodia,
                Autorizacion = autorizacionDto.Codigo // Acceso corregido
            };
        }).ToList();
    }

    public async Task<TraSalidasLaborale?> ObtenerPorIdAsync(int id)
    {
        return await context.TraSalidasLaborales
            .Include(s => s.IdReclusoNavigation)
            .Include(s => s.TraSalidasLaboralesDocumentoRespaldos)
            .FirstOrDefaultAsync(s => s.IdSalida == id);
    }

    public async Task GuardarAsync(
        TraSalidasLaborale entidad,
        List<long> idsDocumentos,
        string codAutorizacion,
        string descAutorizacion,
        string obsUsuario)
    {
        entidad.Observaciones = ConstruirObservaciones(codAutorizacion, descAutorizacion, obsUsuario);

        if (entidad.IdSalida == 0)
        {
            context.TraSalidasLaborales.Add(entidad);
        }
        else
        {
            context.TraSalidasLaborales.Update(entidad);
        }

        await context.SaveChangesAsync();

        if (idsDocumentos != null) // Permitir lista vacía para limpiar, pero no null
        {
            var idsNormalizados = idsDocumentos
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            await ActualizarVinculosDocumentos(entidad.IdSalida, idsNormalizados);
        }
    }

    private async Task ActualizarVinculosDocumentos(int idSalida, List<long> nuevosIds)
    {
        await ValidarIdsDocumentosAsync(nuevosIds);

        var existentes = await context.TraSalidasLaboralesDocumentoRespaldos
            .Where(x => x.IdSalida == idSalida)
            .ToListAsync();

        var aBorrar = existentes
            .Where(x => !nuevosIds.Contains(x.IdDocumento))
            .ToList();

        if (aBorrar.Count > 0)
        {
            context.TraSalidasLaboralesDocumentoRespaldos.RemoveRange(aBorrar);
        }

        var idsExistentes = existentes.Select(x => x.IdDocumento).ToHashSet();
        var aAgregar = nuevosIds.Where(id => !idsExistentes.Contains(id));

        foreach (var idDoc in aAgregar)
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

    public async Task CambiarEstadoAsync(int idSalida, bool activo, string motivo)
    {
        var entidad = await context.TraSalidasLaborales.FindAsync(idSalida);
        if (entidad is null) return;

        entidad.Activo = activo;

        if (!activo && !string.IsNullOrWhiteSpace(motivo))
        {
            // Deserializamos para mantener la estructura JSON válida
            var dto = ParsearObservaciones(entidad.Observaciones);
            dto.Observacion += $"{Environment.NewLine}[{DateTime.Now:g}] Motivo cese: {motivo}";

            // Volvemos a serializar
            entidad.Observaciones = JsonSerializer.Serialize(dto, _jsonOptions);
        }

        await context.SaveChangesAsync();
    }

    public async Task<int> ObtenerDiasAlertaRenovacionesAsync()
    {
        var parametros = await _repoParametros.FindAsync(p => p.Clave == ClaveDiasAlertaRenovaciones);
        var valorParametro = parametros.FirstOrDefault()?.Valor;

        return TryParseDiasAlerta(valorParametro, out var diasAlerta)
            ? diasAlerta
            : DiasAlertaDefecto;
    }

    private static bool TryParseDiasAlerta(string? valor, out int diasAlerta)
    {
        if (int.TryParse(valor, out var parsed) && parsed >= 0)
        {
            diasAlerta = parsed;
            return true;
        }

        diasAlerta = DiasAlertaDefecto;
        return false;
    }

    private static EstadoRenovacion CalcularEstado(bool activa, int diasRestantes, int diasAlerta)
    {
        if (!activa) return EstadoRenovacion.Inactiva;
        if (diasRestantes < 0) return EstadoRenovacion.Vencida;
        if (diasRestantes <= diasAlerta) return EstadoRenovacion.Alerta;
        return EstadoRenovacion.Ok;
    }

    public ObservacionesRenovacionDto ParsearObservaciones(string? obs)
    {
        if (string.IsNullOrWhiteSpace(obs))
            return new ObservacionesRenovacionDto();

        var obsTrimmed = obs.Trim();

        // 1. Intento JSON
        if (obsTrimmed.StartsWith("{") && obsTrimmed.EndsWith("}"))
        {
            try
            {
                var resultado = JsonSerializer.Deserialize<ObservacionesRenovacionDto>(obsTrimmed, _jsonOptions);
                return resultado ?? new ObservacionesRenovacionDto();
            }
            catch
            {
                // Fallback silencioso
            }
        }

        // 2. Fallback Legacy
        return ParsearFormatoLegacy(obs);
    }

    private ObservacionesRenovacionDto ParsearFormatoLegacy(string obs)
    {
        var dto = new ObservacionesRenovacionDto();
        var lineas = obs.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
        var index = 0;

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION#:"))
        {
            dto.Codigo = lineas[index].Replace("#AUTORIZACION#:", string.Empty).Trim();
            index++;
        }

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION_DESC#:"))
        {
            dto.Descripcion = lineas[index].Replace("#AUTORIZACION_DESC#:", string.Empty).Trim();
            index++;
        }

        if (index < lineas.Count)
        {
            dto.Observacion = string.Join(Environment.NewLine, lineas.Skip(index));
        }

        return dto;
    }

    private static string ConstruirObservaciones(string codigo, string descripcion, string observaciones)
    {
        var payload = new ObservacionesRenovacionDto
        {
            Codigo = codigo,
            Descripcion = descripcion,
            Observacion = observaciones
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    public async Task<List<DocumentoRespaldoDto>> BuscarDocumentosCandidatos(int idRecluso, string nombreRecluso)
    {
        var query = context.MaeDocumentos
            .AsNoTracking()
            .Where(d =>
                d.TraSalidasLaborales.Any(s => s.IdRecluso == idRecluso) ||
                d.Asunto.Contains(nombreRecluso) ||
                (d.Descripcion != null && d.Descripcion.Contains(nombreRecluso))
            )
            .OrderByDescending(d => d.FechaCreacion)
            .Take(20)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Descripcion = d.Descripcion,
                NumeroOficial = d.NumeroOficial,
                FechaCreacion = d.FechaCreacion,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            });

        return await query.ToListAsync();
    }

    public async Task<bool> ExisteDocumentoAsync(long idDocumento)
    {
        if (idDocumento <= 0) return false;
        return await context.MaeDocumentos.AsNoTracking().AnyAsync(x => x.IdDocumento == idDocumento);
    }

    public async Task<DocumentoRespaldoDto?> ObtenerDocumentoPorIdAsync(long idDocumento)
    {
        if (idDocumento <= 0) return null;

        return await context.MaeDocumentos
            .AsNoTracking()
            .Where(d => d.IdDocumento == idDocumento)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Descripcion = d.Descripcion,
                NumeroOficial = d.NumeroOficial,
                FechaCreacion = d.FechaCreacion,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            })
            .FirstOrDefaultAsync();
    }

    private async Task ValidarIdsDocumentosAsync(List<long> idsDocumentos)
    {
        if (idsDocumentos == null || idsDocumentos.Count == 0) return;

        var existentes = await context.MaeDocumentos
            .AsNoTracking()
            .Where(d => idsDocumentos.Contains(d.IdDocumento))
            .Select(d => d.IdDocumento)
            .ToListAsync();

        var faltantes = idsDocumentos.Except(existentes).ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException($"No existen documentos con IDs: {string.Join(", ", faltantes)}");
        }
    }
}
