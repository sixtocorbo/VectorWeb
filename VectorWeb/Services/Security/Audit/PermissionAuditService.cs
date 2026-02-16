using Microsoft.Extensions.Logging;
using VectorWeb.Models;

namespace VectorWeb.Services.Security.Audit;

public sealed class PermissionAuditService
{
    private readonly SecretariaDbContext _context;
    private readonly ILogger<PermissionAuditService> _logger;

    public PermissionAuditService(SecretariaDbContext context, ILogger<PermissionAuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RegistrarAccesoDenegadoAsync(int? usuarioId, string? usuarioNombre, string? rol, string permisoRequerido, string? modulo, string? ruta)
    {
        try
        {
            var evento = new SegAuditoriaPermiso
            {
                UsuarioId = usuarioId,
                UsuarioNombre = string.IsNullOrWhiteSpace(usuarioNombre) ? null : usuarioNombre,
                Rol = string.IsNullOrWhiteSpace(rol) ? null : rol,
                PermisoRequerido = permisoRequerido,
                Modulo = string.IsNullOrWhiteSpace(modulo) ? null : modulo,
                Ruta = string.IsNullOrWhiteSpace(ruta) ? null : ruta,
                FechaEvento = DateTime.UtcNow
            };

            _context.SegAuditoriaPermisos.Add(evento);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar la auditoría de permiso denegado para usuario {UsuarioNombre}.", usuarioNombre ?? "desconocido");
        }
    }
}
