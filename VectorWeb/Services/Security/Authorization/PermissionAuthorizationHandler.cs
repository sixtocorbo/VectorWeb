using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace VectorWeb.Services.Security.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly RolePermissionService _rolePermissionService;

    public PermissionAuthorizationHandler(RolePermissionService rolePermissionService)
    {
        _rolePermissionService = rolePermissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var permisos = await _rolePermissionService.ObtenerPermisosPorRolAsync(role);

        if (permisos.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
