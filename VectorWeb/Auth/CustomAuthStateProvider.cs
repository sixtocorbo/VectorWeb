using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;
using VectorWeb.Services.Security;

namespace VectorWeb.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string SessionKey = "vectorweb.auth.user";
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly RolePermissionService _rolePermissionService;
    private bool _initialized;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public CustomAuthStateProvider(ProtectedSessionStorage sessionStorage, RolePermissionService rolePermissionService)
    {
        _sessionStorage = sessionStorage;
        _rolePermissionService = rolePermissionService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = await TryRestoreUserFromSessionAsync();
        }

        return new AuthenticationState(_currentUser);
    }

    public async Task MarkUserAsAuthenticatedAsync(int idUsuario, string nombre, string? rol, int idOficina)
    {
        var roleValue = string.IsNullOrWhiteSpace(rol) ? "OPERADOR" : rol.Trim().ToUpperInvariant();
        var authUser = new AuthSessionUser(idUsuario, nombre, roleValue, idOficina);

        await _sessionStorage.SetAsync(SessionKey, authUser);

        var user = await ConstruirPrincipalAsync(idUsuario, nombre, roleValue, idOficina);
        _currentUser = user;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }


    public async Task RefreshCurrentUserPermissionsAsync()
    {
        if (_currentUser.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var idClaim = _currentUser.FindFirstValue(ClaimTypes.NameIdentifier);
        var nameClaim = _currentUser.FindFirstValue(ClaimTypes.Name);
        var roleClaim = _currentUser.FindFirstValue(ClaimTypes.Role);
        var oficinaClaim = _currentUser.FindFirstValue("IdOficina");

        if (!int.TryParse(idClaim, out var idUsuario) ||
            !int.TryParse(oficinaClaim, out var idOficina) ||
            string.IsNullOrWhiteSpace(nameClaim))
        {
            return;
        }

        await _rolePermissionService.RevalidateCacheAsync();
        _currentUser = await ConstruirPrincipalAsync(idUsuario, nameClaim, roleClaim ?? "OPERADOR", idOficina);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
    public async Task MarkUserAsLoggedOutAsync()
    {
        await _sessionStorage.DeleteAsync(SessionKey);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task<bool> TryRestoreUserFromSessionAsync()
    {
        try
        {
            var sessionResult = await _sessionStorage.GetAsync<AuthSessionUser>(SessionKey);
            if (sessionResult.Success && sessionResult.Value is not null)
            {
                _currentUser = await ConstruirPrincipalAsync(
                    sessionResult.Value.IdUsuario,
                    sessionResult.Value.Nombre,
                    sessionResult.Value.Rol,
                    sessionResult.Value.IdOficina);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            // Browser storage is unavailable during server pre-rendering.
            return false;
        }
    }

    private async Task<ClaimsPrincipal> ConstruirPrincipalAsync(int idUsuario, string nombre, string rol, int idOficina)
    {
        var permisos = await _rolePermissionService.ObtenerPermisosPorRolAsync(rol);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new(ClaimTypes.Name, nombre),
            new(ClaimTypes.Role, rol),
            new("IdOficina", idOficina.ToString())
        };

        claims.AddRange(permisos.Select(permiso => new Claim(AppPermissions.ClaimType, permiso)));

        var identity = new ClaimsIdentity(claims, "CustomAuth");
        return new ClaimsPrincipal(identity);
    }

    private sealed record AuthSessionUser(int IdUsuario, string Nombre, string Rol, int IdOficina);
}
