using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using VectorWeb.Services.Security.Audit;

namespace VectorWeb.Services.Security.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly RolePermissionService _rolePermissionService;
    private readonly PermissionAuditService _permissionAuditService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        RolePermissionService rolePermissionService,
        PermissionAuditService permissionAuditService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _rolePermissionService = rolePermissionService;
        _permissionAuditService = permissionAuditService;
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

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = int.TryParse(userIdClaim, out var userId);
        var (modulo, ruta) = ObtenerContextoRecurso(context.Resource);

        _logger.LogWarning(
            "Acceso denegado para el usuario {UserName}. Rol: {Role}. Permiso requerido: {RequiredPermission}. Ruta: {Ruta}",
            userName,
            role ?? "sin_rol",
            requirement.Permission,
            ruta ?? "desconocida");

        await _permissionAuditService.RegistrarAccesoDenegadoAsync(
            userId > 0 ? userId : null,
            userName,
            role,
            requirement.Permission,
            modulo,
            ruta);
    }

    private static (string? modulo, string? ruta) ObtenerContextoRecurso(object? resource)
    {
        if (resource is HttpContext httpContext)
        {
            var endpoint = httpContext.GetEndpoint();
            var endpointName = endpoint?.DisplayName;
            var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value : null;
            return (endpointName, path);
        }

        if (resource is Microsoft.AspNetCore.Components.RouteData routeData)
        {
            var route = routeData.RouteValues.TryGetValue("page", out var page)
                ? page?.ToString()
                : routeData.PageType?.Name;
            return (routeData.PageType?.Name, route);
        }

        return (resource?.GetType().Name, null);
    }
}
