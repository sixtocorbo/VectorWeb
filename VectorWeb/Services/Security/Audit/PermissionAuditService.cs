using Microsoft.Extensions.Logging;
using VectorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace VectorWeb.Services.Security.Audit;

public sealed class PermissionAuditService
{
    private readonly IDbContextFactory<SecretariaDbContext> _dbContextFactory;
    private readonly ILogger<PermissionAuditService> _logger;

    public PermissionAuditService(IDbContextFactory<SecretariaDbContext> dbContextFactory, ILogger<PermissionAuditService> logger)
    {
        _dbContextFactory = dbContextFactory;
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

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            context.SegAuditoriaPermisos.Add(evento);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar la auditoría de permiso denegado para usuario {UsuarioNombre}.", usuarioNombre ?? "desconocido");
        }
    }
}
