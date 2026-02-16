using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VectorWeb.Models;

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

public sealed class ObservacionesRenovacionDto
{
    public string CodigoAutorizacion { get; set; } = string.Empty;
    public string DescripcionAutorizacion { get; set; } = string.Empty;
    public string ObservacionesUsuario { get; set; } = string.Empty;
}

public sealed class DocumentoRespaldoDto
{
    public long IdDocumento { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string? Asunto { get; set; }
}

public sealed class RenovacionesService
{
    private readonly SecretariaDbContext context;
    private const int DiasAlertaDefecto = 30;

    // Cacheamos las opciones de serialización para no instanciarlas en cada llamada
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public RenovacionesService(SecretariaDbContext context)
    {
        this.context = context;
    }

    public async Task<List<SalidaGridDto>> ObtenerSalidasAsync(bool soloActivas, string textoBuscar)
    {
        var hoy = DateTime.Today;

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

        // Procesamiento en memoria optimizado
        return datos.Select(x =>
        {
            var dias = (x.Salida.FechaVencimiento.Date - hoy).Days;
            var activa = x.Salida.Activo ?? true;
            var estado = CalcularEstado(activa, dias);
            var autorizacion = ParsearObservaciones(x.Salida.Observaciones);

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
                Autorizacion = autorizacion.CodigoAutorizacion // Corregido para usar la propiedad correcta del DTO
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

        // Manejo seguro de la lista de documentos
        if (idsDocumentos != null && idsDocumentos.Count > 0)
        {
            var idsNormalizados = idsDocumentos
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            await ActualizarVinculosDocumentos(entidad.IdSalida, idsNormalizados);
        }
        else
        {
            // Si la lista viene vacía, asegurarse de limpiar vínculos existentes si es necesario
            // Opcional: depende de la regla de negocio. Aquí asumo que si viene vacía o nula no tocamos nada o borramos todo.
            // Para seguridad, si es explícitamente una lista vacía, borramos los vínculos.
            if (idsDocumentos != null && idsDocumentos.Count == 0)
            {
                await ActualizarVinculosDocumentos(entidad.IdSalida, new List<long>());
            }
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
            // Nota: Aquí simplemente concatenamos texto al final, el JSON previo (si existe) se rompe.
            // MEJORA: Deberíamos deserializar, agregar el motivo al campo ObservacionesUsuario y volver a serializar.
            // Por simplicidad mantenemos tu lógica, pero ten en cuenta que esto invalidará el JSON para futuras lecturas.
            var dto = ParsearObservaciones(entidad.Observaciones);
            dto.ObservacionesUsuario += $"{Environment.NewLine}[{DateTime.Now:g}] Motivo cese: {motivo}";
            entidad.Observaciones = JsonSerializer.Serialize(dto, _jsonOptions);
        }

        await context.SaveChangesAsync();
    }

    // Método estático puro para calcular estado
    private static EstadoRenovacion CalcularEstado(bool activa, int diasRestantes)
    {
        if (!activa) return EstadoRenovacion.Inactiva;
        if (diasRestantes < 0) return EstadoRenovacion.Vencida;
        if (diasRestantes <= DiasAlertaDefecto) return EstadoRenovacion.Alerta;
        return EstadoRenovacion.Ok;
    }

    public ObservacionesRenovacionDto ParsearObservaciones(string? obs)
    {
        if (string.IsNullOrWhiteSpace(obs))
            return new ObservacionesRenovacionDto();

        var obsTrimmed = obs.Trim();

        // 1. Intento Rápido: Verificar si parece JSON
        if (obsTrimmed.StartsWith("{") && obsTrimmed.EndsWith("}"))
        {
            try
            {
                var resultado = JsonSerializer.Deserialize<ObservacionesRenovacionDto>(obsTrimmed, _jsonOptions);
                return resultado ?? new ObservacionesRenovacionDto();
            }
            catch
            {
                // Si falla el JSON (formato corrupto), caemos al legacy por seguridad
            }
        }

        // 2. Fallback: Formato Legacy
        return ParsearFormatoLegacy(obs);
    }

    private ObservacionesRenovacionDto ParsearFormatoLegacy(string obs)
    {
        var dto = new ObservacionesRenovacionDto();
        var lineas = obs.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
        var index = 0;

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION#:"))
        {
            dto.CodigoAutorizacion = lineas[index].Replace("#AUTORIZACION#:", string.Empty).Trim();
            index++;
        }

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION_DESC#:"))
        {
            dto.DescripcionAutorizacion = lineas[index].Replace("#AUTORIZACION_DESC#:", string.Empty).Trim();
            index++;
        }

        if (index < lineas.Count)
        {
            dto.ObservacionesUsuario = string.Join(Environment.NewLine, lineas.Skip(index));
        }

        return dto;
    }

    private static string ConstruirObservaciones(string codigo, string descripcion, string observaciones)
    {
        var payload = new ObservacionesRenovacionDto
        {
            CodigoAutorizacion = codigo,
            DescripcionAutorizacion = descripcion,
            ObservacionesUsuario = observaciones
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    public async Task<List<DocumentoRespaldoDto>> BuscarDocumentosCandidatos(int idRecluso, string nombreRecluso)
    {
        // MEJORA: Usar una sola consulta con OR en lugar de UNION en memoria
        // Esto permite que SQL Server optimice el plan de ejecución
        var query = context.MaeDocumentos
            .AsNoTracking()
            .Where(d =>
                d.TraSalidasLaborales.Any(s => s.IdRecluso == idRecluso) || // Relacionados
                d.Asunto.Contains(nombreRecluso) ||                        // Por asunto
                (d.Descripcion != null && d.Descripcion.Contains(nombreRecluso)) // Por descripción
            )
            .OrderByDescending(d => d.FechaCreacion)
            .Take(20)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            });

        return await query.ToListAsync();
    }

    public async Task<bool> ExisteDocumentoAsync(long idDocumento)
    {
        if (idDocumento <= 0) return false;
        return await context.MaeDocumentos.AsNoTracking().AnyAsync(x => x.IdDocumento == idDocumento);
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