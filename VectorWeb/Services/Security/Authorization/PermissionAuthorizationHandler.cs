using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace VectorWeb.Services.Security.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly RolePermissionService _rolePermissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(RolePermissionService rolePermissionService, ILogger<PermissionAuthorizationHandler> logger)
    {
        _rolePermissionService = rolePermissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userName = context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "desconocido";
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var permisos = await _rolePermissionService.ObtenerPermisosPorRolAsync(role);

        if (permisos.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogWarning(
            "Acceso denegado para el usuario {UserName}. Rol: {Role}. Permiso requerido: {RequiredPermission}.",
            userName,
            role ?? "sin_rol",
            requirement.Permission);
    }
}
