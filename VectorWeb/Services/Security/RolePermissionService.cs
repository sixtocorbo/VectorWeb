using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VectorWeb.Models;
using VectorWeb.Repositories;

namespace VectorWeb.Services.Security;

public class RolePermissionService
{
    private const string ParametroPermisos = "SEGURIDAD_PERMISOS_POR_ROL";
    private const string MatrizCacheKey = "security.permissions.matrix";
    private static readonly TimeSpan MatrizCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IRepository<CfgSistemaParametro> _repoParametros;
    private readonly IMemoryCache _cache;

    public RolePermissionService(IRepository<CfgSistemaParametro> repoParametros, IMemoryCache cache)
    {
        _repoParametros = repoParametros;
        _cache = cache;
    }

    public async Task<Dictionary<string, HashSet<string>>> ObtenerMatrizAsync(bool forzarRefresco = false)
    {
        if (forzarRefresco)
        {
            _cache.Remove(MatrizCacheKey);
        }

        if (_cache.TryGetValue(MatrizCacheKey, out Dictionary<string, HashSet<string>>? matrizCache)
            && matrizCache is not null)
        {
            return ClonarMatriz(matrizCache);
        }

        var defaults = ObtenerMatrizDefault();
        var parametro = (await _repoParametros.FindAsync(p => p.Clave == ParametroPermisos)).FirstOrDefault();

        if (parametro is null || string.IsNullOrWhiteSpace(parametro.Valor))
        {
            _cache.Set(MatrizCacheKey, ClonarMatriz(defaults), MatrizCacheTtl);
            return defaults;
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parametro.Valor);
            if (raw is null || raw.Count == 0)
            {
                _cache.Set(MatrizCacheKey, ClonarMatriz(defaults), MatrizCacheTtl);
                return defaults;
            }

            var matriz = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (rol, permisos) in raw)
            {
                var rolNormalizado = NormalizarRol(rol);
                if (string.IsNullOrWhiteSpace(rolNormalizado))
                {
                    continue;
                }

                var permisosValidos = permisos?
                    .Where(p => AppPermissions.Todos.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .Select(p => p.Trim().ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? [];

                matriz[rolNormalizado] = permisosValidos;
            }

            foreach (var (rolDefault, permisosDefault) in defaults)
            {
                if (!matriz.TryGetValue(rolDefault, out var permisosRol) || permisosRol.Count == 0)
                {
                    matriz[rolDefault] = new HashSet<string>(permisosDefault, StringComparer.OrdinalIgnoreCase);
                }
            }

            _cache.Set(MatrizCacheKey, ClonarMatriz(matriz), MatrizCacheTtl);
            return matriz;
        }
        catch (JsonException)
        {
            _cache.Set(MatrizCacheKey, ClonarMatriz(defaults), MatrizCacheTtl);
            return defaults;
        }
    }

    public async Task RevalidateCacheAsync()
    {
        _cache.Remove(MatrizCacheKey);
        await ObtenerMatrizAsync(forzarRefresco: true);
    }

    public async Task<HashSet<string>> ObtenerPermisosPorRolAsync(string? rol)
    {
        var matriz = await ObtenerMatrizAsync();
        var rolNormalizado = NormalizarRol(rol);

        if (matriz.TryGetValue(rolNormalizado, out var permisos))
        {
            return new HashSet<string>(permisos, StringComparer.OrdinalIgnoreCase);
        }

        return ObtenerPermisosDefaultPorTipoRol(rolNormalizado);
    }

    public HashSet<string> ObtenerPermisosDefaultPorTipoRol(string? rol)
    {
        var defaults = ObtenerMatrizDefault();
        var rolNormalizado = NormalizarRol(rol);

        if (!string.IsNullOrWhiteSpace(rolNormalizado) &&
            defaults.TryGetValue(rolNormalizado, out var permisosPorDefault) &&
            permisosPorDefault.Count > 0)
        {
            return new HashSet<string>(permisosPorDefault, StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(defaults["OPERADOR"], StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizarRol(string? rol)
        => string.IsNullOrWhiteSpace(rol) ? "OPERADOR" : rol.Trim().ToUpperInvariant();

    public static bool EsRolAdministrativo(string? rol)
    {
        var rolNormalizado = NormalizarRol(rol);
        return rolNormalizado is "ADMIN" or "ADMINISTRADOR" or "SUPERADMIN";
    }

    public string SerializarMatriz(Dictionary<string, HashSet<string>> matriz)
    {
        var payload = matriz
            .OrderBy(x => x.Key)
            .ToDictionary(
                x => x.Key,
                x => x.Value.OrderBy(v => v).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(payload);
    }

    public static Dictionary<string, HashSet<string>> ObtenerMatrizDefault()
    {
        return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase),
            ["ADMINISTRADOR"] = AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase),
            ["SUPERADMIN"] = AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase),
            ["OPERADOR"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppPermissions.DocumentosVer,
                AppPermissions.DocumentosEditar,
                AppPermissions.VinculacionGestionar,
                AppPermissions.ReclusosGestionar,
                AppPermissions.RenovacionesGestionar
            }
        };
    }

    private static Dictionary<string, HashSet<string>> ClonarMatriz(Dictionary<string, HashSet<string>> source)
        => source.ToDictionary(
            x => x.Key,
            x => new HashSet<string>(x.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
}
