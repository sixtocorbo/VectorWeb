using VectorWeb.Models;
using VectorWeb.Repositories;
using VectorWeb.Utils;

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
        if (idTipoDocumento <= 0) return null;

        var prioridadNormalizada = string.IsNullOrWhiteSpace(prioridad)
            ? "NORMAL"
            : prioridad.Trim().ToUpperInvariant();

        // USAMOS EL MÉTODO BLINDADO: GetFilteredAsync
        // Esto asegura que el contexto se abra y cierre dentro del repositorio
        var configuraciones = await _repoPlazos.GetFilteredAsync(c => c.IdTipoDocumento == idTipoDocumento);

        var exacta = configuraciones.FirstOrDefault(c =>
            string.Equals((c.Prioridad ?? string.Empty).Trim(), prioridadNormalizada, StringComparison.OrdinalIgnoreCase));

        if (exacta is not null) return exacta.DiasMaximos;

        var normal = configuraciones.FirstOrDefault(c =>
            string.Equals((c.Prioridad ?? string.Empty).Trim(), "NORMAL", StringComparison.OrdinalIgnoreCase));

        return normal?.DiasMaximos;
    }

    public async Task<DateTime?> CalcularFechaVencimientoAsync(int idTipoDocumento, DateTime fechaBase, string prioridad = "NORMAL", ISet<DateOnly>? feriados = null)
    {
        var dias = await ObtenerDiasPlazoAsync(idTipoDocumento, prioridad);
        if (!dias.HasValue) return null;

        return CalcularFechaVencimiento(fechaBase, dias.Value, feriados);
    }

    public DateTime CalcularFechaVencimiento(DateTime fechaBase, int diasPlazo, ISet<DateOnly>? feriados = null)
    {
        // El ayudante de fechas que revisamos ayer
        return diasPlazo < 30
            ? fechaBase.AgregarDiasHabiles(diasPlazo, feriados)
            : fechaBase.AgregarDiasCorridos(diasPlazo);
    }
}