using Microsoft.EntityFrameworkCore;
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
    public string Estado { get; set; } = "OK";
    public bool Activo { get; set; }
    public int CantidadDocumentos { get; set; }
    public string? DetalleCustodia { get; set; }
    public string? Autorizacion { get; set; }
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

            var estado = "OK";
            if (!activa)
            {
                estado = "INACTIVA";
            }
            else if (dias < 0)
            {
                estado = "VENCIDA";
            }
            else if (dias <= DiasAlertaDefecto)
            {
                estado = "ALERTA";
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

        var existentes = context.TraSalidasLaboralesDocumentoRespaldos.Where(x => x.IdSalida == entidad.IdSalida);
        context.TraSalidasLaboralesDocumentoRespaldos.RemoveRange(existentes);

        foreach (var idDoc in idsDocumentos.Distinct())
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
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            sb.AppendLine($"#AUTORIZACION#:{codigo}");
        }

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            sb.AppendLine($"#AUTORIZACION_DESC#:{descripcion}");
        }

        sb.Append(observaciones);
        return sb.ToString();
    }

    public async Task<List<DocumentoRespaldoDto>> BuscarDocumentosCandidatos(int idRecluso, string nombreRecluso)
    {
        var _ = idRecluso;

        var documentos = await context.MaeDocumentos
            .AsNoTracking()
            .Where(d => d.Asunto.Contains(nombreRecluso) || (d.Descripcion != null && d.Descripcion.Contains(nombreRecluso)))
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
}
