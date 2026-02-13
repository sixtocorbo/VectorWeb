using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;

namespace VectorWeb.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string SessionKey = "vectorweb.auth.user";
    private readonly ProtectedSessionStorage _sessionStorage;
    private bool _initialized;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public CustomAuthStateProvider(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            await TryRestoreUserFromSessionAsync();
        }

        return new AuthenticationState(_currentUser);
    }

    public async Task MarkUserAsAuthenticatedAsync(int idUsuario, string nombre, string? rol, int idOficina)
    {
        var roleValue = string.IsNullOrWhiteSpace(rol) ? "Usuario" : rol;
        var authUser = new AuthSessionUser(idUsuario, nombre, roleValue, idOficina);

        await _sessionStorage.SetAsync(SessionKey, authUser);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Role, roleValue),
            new Claim("IdOficina", idOficina.ToString())
        }, "CustomAuth");

        var user = new ClaimsPrincipal(identity);
        _currentUser = user;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _sessionStorage.DeleteAsync(SessionKey);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task TryRestoreUserFromSessionAsync()
    {
        try
        {
            var sessionResult = await _sessionStorage.GetAsync<AuthSessionUser>(SessionKey);
            if (sessionResult.Success && sessionResult.Value is not null)
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, sessionResult.Value.IdUsuario.ToString()),
                    new Claim(ClaimTypes.Name, sessionResult.Value.Nombre),
                    new Claim(ClaimTypes.Role, sessionResult.Value.Rol),
                    new Claim("IdOficina", sessionResult.Value.IdOficina.ToString())
                }, "CustomAuth");

                _currentUser = new ClaimsPrincipal(identity);
            }
        }
        catch (InvalidOperationException)
        {
            // Browser storage is unavailable during server pre-rendering.
        }
    }

    private sealed record AuthSessionUser(int IdUsuario, string Nombre, string Rol, int IdOficina);
}
