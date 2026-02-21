using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services.Security.Audit;

public sealed class PermissionAuditService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly ILogger<PermissionAuditService> _logger;

    public PermissionAuditService(IDbContextFactory<SecretariaDbContext> contextFactory, ILogger<PermissionAuditService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // Método general que ya tenías
    public async Task RegistrarAccesoAsync(int usuarioId, string nombre, string rol, string permiso, string ruta, bool concedido)
    {
        await RegistrarAccesoDenegadoAsync(usuarioId, nombre, rol, permiso, DeterminarModulo(ruta), ruta);
    }

    // NUEVO: Método específico que busca el PermissionAuthorizationHandler
    public async Task RegistrarAccesoDenegadoAsync(int? usuarioId, string nombre, string? rol, string permiso, string? modulo, string? ruta)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var auditoria = new SegAuditoriaPermiso
            {
                UsuarioId = usuarioId ?? 0,
                UsuarioNombre = nombre,
                Rol = rol ?? "ANÓNIMO",
                PermisoRequerido = permiso,
                Ruta = ruta ?? "N/D",
                FechaEvento = DateTime.Now,
                Modulo = modulo ?? DeterminarModulo(ruta ?? "")
            };

            context.SegAuditoriaPermisos.Add(auditoria);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al registrar auditoría de acceso denegado para {Nombre}", nombre);
        }
    }

    private static string DeterminarModulo(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return "GENERAL";
        var r = ruta.ToUpperInvariant();
        if (r.Contains("/DOCUMENTOS")) return "DOCUMENTOS";
        if (r.Contains("/CONFIGURACION")) return "CONFIGURACIÓN";
        if (r.Contains("/RECLUSOS")) return "RECLUSOS";
        return "OTROS";
    }
}