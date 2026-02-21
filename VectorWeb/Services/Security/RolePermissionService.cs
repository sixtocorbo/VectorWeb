using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using VectorWeb.Models;

namespace VectorWeb.Services.Security;

public sealed class RolePermissionService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private const string CacheKeyPrefix = "permisos_rol_";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    public RolePermissionService(IDbContextFactory<SecretariaDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public async Task<List<string>> ObtenerPermisosPorRolAsync(string rol)
    {
        if (string.IsNullOrWhiteSpace(rol)) return new List<string>();
        var key = $"{CacheKeyPrefix}{NormalizarRol(rol)}";

        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            var matriz = await ObtenerMatrizAsync();

            if (matriz.TryGetValue(rol, out var permisos))
            {
                return permisos.ToList();
            }

            return ObtenerPermisosDefaultPorTipoRol(rol).ToList();
        }) ?? new List<string>();
    }

    public async Task RevalidateCacheAsync()
    {
        // En una implementación simple, esto podría limpiar la caché
        await Task.CompletedTask;
    }

    // --- MÉTODOS REQUERIDOS POR LA INTERFAZ DE USUARIOS Y ROLES ---

    public static string NormalizarRol(string? rol) =>
        string.IsNullOrWhiteSpace(rol) ? "OPERADOR" : rol.Trim().ToUpperInvariant();

    public bool EsRolAdministrativo(string? rol)
    {
        var r = NormalizarRol(rol);
        return r == "ADMIN" || r == "ADMINISTRADOR" || r == "SUPERADMIN";
    }

    public async Task<Dictionary<string, HashSet<string>>> ObtenerMatrizAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var parametro = await context.CfgSistemaParametros
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Clave == "SEGURIDAD_PERMISOS_POR_ROL");

        if (parametro == null || string.IsNullOrWhiteSpace(parametro.Valor))
            return ObtenerMatrizDefault();

        if (!parametro.Valor.Trim().StartsWith("{"))
            return ObtenerMatrizDefault();

        try
        {
            var matrizRaw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parametro.Valor);

            if (matrizRaw == null) return ObtenerMatrizDefault();

            // CORRECCIÓN AQUÍ: Usamos v.Value para obtener la List<string>
            return matrizRaw.ToDictionary(
                k => k.Key,
                v => new HashSet<string>(v.Value, StringComparer.OrdinalIgnoreCase)
            );
        }
        catch (JsonException)
        {
            return ObtenerMatrizDefault();
        }
    }

    public string SerializarMatriz(Dictionary<string, HashSet<string>> matriz) =>
        JsonSerializer.Serialize(matriz);

    public Dictionary<string, HashSet<string>> ObtenerMatrizDefault()
    {
        return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMINISTRADOR"] = new HashSet<string>(AppPermissions.Todos),
            ["OPERADOR"] = new HashSet<string> { AppPermissions.DocumentosVer, AppPermissions.DocumentosEditar }
        };
    }

    public HashSet<string> ObtenerPermisosDefaultPorTipoRol(string rol)
    {
        var r = NormalizarRol(rol);
        if (EsRolAdministrativo(r))
        {
            return new HashSet<string>(AppPermissions.Todos);
        }
        return new HashSet<string> { AppPermissions.DocumentosVer, AppPermissions.DocumentosEditar };
    }
}