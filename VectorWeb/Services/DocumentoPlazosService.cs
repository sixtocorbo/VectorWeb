using VectorWeb.Models;
using VectorWeb.Repositories;

namespace VectorWeb.Services;

public class DocumentoPlazosService
{
    private readonly IRepository<CfgTiemposRespuestum> _repoPlazos;

    public DocumentoPlazosService(IRepository<CfgTiemposRespuestum> repoPlazos)
    {
        _repoPlazos = repoPlazos;
    }

    public async Task<int?> ObtenerDiasPlazoAsync(int idTipoDocumento, string prioridad = "NORMAL")
    {
        if (idTipoDocumento <= 0)
        {
            return null;
        }

        var prioridadNormalizada = string.IsNullOrWhiteSpace(prioridad)
            ? "NORMAL"
            : prioridad.Trim().ToUpperInvariant();

        var configuraciones = await _repoPlazos.FindAsync(c => c.IdTipoDocumento == idTipoDocumento);

        var exacta = configuraciones.FirstOrDefault(c =>
            string.Equals((c.Prioridad ?? string.Empty).Trim(), prioridadNormalizada, StringComparison.OrdinalIgnoreCase));

        if (exacta is not null)
        {
            return exacta.DiasMaximos;
        }

        var normal = configuraciones.FirstOrDefault(c =>
            string.Equals((c.Prioridad ?? string.Empty).Trim(), "NORMAL", StringComparison.OrdinalIgnoreCase));

        if (normal is not null)
        {
            return normal.DiasMaximos;
        }

        return configuraciones.OrderBy(c => c.DiasMaximos).Select(c => (int?)c.DiasMaximos).FirstOrDefault();
    }

    public async Task<DateTime?> CalcularFechaVencimientoAsync(int idTipoDocumento, DateTime fechaBase, string prioridad = "NORMAL")
    {
        var dias = await ObtenerDiasPlazoAsync(idTipoDocumento, prioridad);
        return dias.HasValue ? fechaBase.Date.AddDays(dias.Value) : null;
    }
}
