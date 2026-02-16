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

        return datos.Select(x =>
        {
            var dias = (x.Salida.FechaVencimiento.Date - hoy).Days;
            var activa = x.Salida.Activo ?? true;

            var estado = EstadoRenovacion.Ok;
            if (!activa)
            {
                estado = EstadoRenovacion.Inactiva;
            }
            else if (dias < 0)
            {
                estado = EstadoRenovacion.Vencida;
            }
            else if (dias <= DiasAlertaDefecto)
            {
                estado = EstadoRenovacion.Alerta;
            }

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
                Autorizacion = autorizacion.Codigo
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

        var idsNormalizados = idsDocumentos
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        await ValidarIdsDocumentosAsync(idsNormalizados);

        var existentes = await context.TraSalidasLaboralesDocumentoRespaldos
            .Where(x => x.IdSalida == entidad.IdSalida)
            .ToListAsync();

        var aBorrar = existentes
            .Where(x => !idsNormalizados.Contains(x.IdDocumento))
            .ToList();

        if (aBorrar.Count > 0)
        {
            context.TraSalidasLaboralesDocumentoRespaldos.RemoveRange(aBorrar);
        }

        var idsExistentes = existentes.Select(x => x.IdDocumento).ToHashSet();
        var aAgregar = idsNormalizados.Where(id => !idsExistentes.Contains(id));

        foreach (var idDoc in aAgregar)
        {
            context.TraSalidasLaboralesDocumentoRespaldos.Add(new TraSalidasLaboralesDocumentoRespaldo
            {
                IdSalida = entidad.IdSalida,
                IdDocumento = idDoc,
                FechaRegistro = DateTime.Now
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task CambiarEstadoAsync(int idSalida, bool activo, string motivo)
    {
        var entidad = await context.TraSalidasLaborales.FindAsync(idSalida);
        if (entidad is null)
        {
            return;
        }

        entidad.Activo = activo;

        if (!activo && !string.IsNullOrWhiteSpace(motivo))
        {
            entidad.Observaciones = $"{entidad.Observaciones}{Environment.NewLine}[{DateTime.Now:g}] Motivo cese: {motivo}";
        }

        await context.SaveChangesAsync();
    }

    public (string Codigo, string Descripcion, string Observacion) ParsearObservaciones(string? obs)
    {
        var codigo = string.Empty;
        var desc = string.Empty;
        var usuario = string.Empty;

        if (string.IsNullOrEmpty(obs))
        {
            return (codigo, desc, usuario);
        }

        if (IntentarParsearJsonObservaciones(obs, out var jsonObs))
        {
            return (jsonObs.CodigoAutorizacion, jsonObs.DescripcionAutorizacion, jsonObs.ObservacionesUsuario);
        }

        var lineas = obs.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
        var index = 0;

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION#:"))
        {
            codigo = lineas[index].Replace("#AUTORIZACION#:", string.Empty).Trim();
            index++;
        }

        if (lineas.Count > index && lineas[index].StartsWith("#AUTORIZACION_DESC#:"))
        {
            desc = lineas[index].Replace("#AUTORIZACION_DESC#:", string.Empty).Trim();
            index++;
        }

        if (index < lineas.Count)
        {
            usuario = string.Join(Environment.NewLine, lineas.Skip(index));
        }

        return (codigo, desc, usuario);
    }

    private static string ConstruirObservaciones(string codigo, string descripcion, string observaciones)
    {
        var payload = new ObservacionesRenovacionDto
        {
            CodigoAutorizacion = codigo,
            DescripcionAutorizacion = descripcion,
            ObservacionesUsuario = observaciones
        };

        return JsonSerializer.Serialize(payload);
    }

    public async Task<List<DocumentoRespaldoDto>> BuscarDocumentosCandidatos(int idRecluso, string nombreRecluso)
    {
        var documentosRelacionados = context.MaeDocumentos
            .AsNoTracking()
            .Where(d => d.TraSalidasLaborales.Any(s => s.IdRecluso == idRecluso));

        var porTexto = context.MaeDocumentos
            .AsNoTracking()
            .Where(d => d.Asunto.Contains(nombreRecluso) || (d.Descripcion != null && d.Descripcion.Contains(nombreRecluso)));

        var documentos = await documentosRelacionados
            .Union(porTexto)
            .OrderByDescending(d => d.FechaCreacion)
            .Take(20)
            .Select(d => new DocumentoRespaldoDto
            {
                IdDocumento = d.IdDocumento,
                Asunto = d.Asunto,
                Texto = $"DOC {d.NumeroOficial ?? "S/N"} | {d.Asunto}"
            })
            .ToListAsync();

        return documentos;
    }

    public async Task<bool> ExisteDocumentoAsync(long idDocumento)
    {
        if (idDocumento <= 0)
        {
            return false;
        }

        return await context.MaeDocumentos.AsNoTracking().AnyAsync(x => x.IdDocumento == idDocumento);
    }

    private async Task ValidarIdsDocumentosAsync(List<long> idsDocumentos)
    {
        if (idsDocumentos.Count == 0)
        {
            return;
        }

        var existentes = await context.MaeDocumentos
            .AsNoTracking()
            .Where(d => idsDocumentos.Contains(d.IdDocumento))
            .Select(d => d.IdDocumento)
            .ToListAsync();

        var faltantes = idsDocumentos.Except(existentes).ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException($"No existen documentos: {string.Join(", ", faltantes)}");
        }
    }

    private static bool IntentarParsearJsonObservaciones(string obs, out ObservacionesRenovacionDto dto)
    {
        dto = new ObservacionesRenovacionDto();

        if (string.IsNullOrWhiteSpace(obs))
        {
            return false;
        }

        var valorNormalizado = obs.Trim();
        if (!valorNormalizado.StartsWith('{') || !valorNormalizado.EndsWith('}'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(valorNormalizado);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            dto = document.RootElement.Deserialize<ObservacionesRenovacionDto>() ?? new ObservacionesRenovacionDto();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
