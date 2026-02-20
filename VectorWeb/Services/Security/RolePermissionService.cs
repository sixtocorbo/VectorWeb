using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VectorWeb.Models;

namespace VectorWeb.Services.Security;

public class RolePermissionService
{
    private const string ParametroPermisos = "SEGURIDAD_PERMISOS_POR_ROL";
    private const string MatrizCacheKey = "security.permissions.matrix";
    private static readonly TimeSpan MatrizCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlyList<string> PermisosOrdenados = AppPermissions.Todos
        .OrderBy(permiso => permiso, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static readonly IReadOnlyDictionary<int, string> LegacyIndiceAPermiso = PermisosOrdenados
        .Select((permiso, indice) => new { permiso, indice })
        .ToDictionary(x => x.indice, x => x.permiso);

    private static readonly IReadOnlyDictionary<string, string> PermisoCanonical = AppPermissions.Todos
        .ToDictionary(permiso => permiso, permiso => permiso, StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<SecretariaDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;

    public RolePermissionService(IDbContextFactory<SecretariaDbContext> dbContextFactory, IMemoryCache cache)
    {
        _dbContextFactory = dbContextFactory;
        _cache = cache;
    }

    public async Task<Dictionary<string, HashSet<string>>> ObtenerMatrizAsync(bool forzarRefresco = false)
    {
        if (!forzarRefresco
            && _cache.TryGetValue(MatrizCacheKey, out Dictionary<string, HashSet<string>>? matrizCache)
            && matrizCache is not null)
        {
            return ClonarMatriz(matrizCache);
        }

        if (forzarRefresco)
        {
            _cache.Remove(MatrizCacheKey);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var valorParametro = await context.CfgSistemaParametros
            .AsNoTracking()
            .Where(p => p.Clave == ParametroPermisos)
            .Select(p => p.Valor)
            .FirstOrDefaultAsync();

        var defaults = ObtenerMatrizDefault();

        if (string.IsNullOrWhiteSpace(valorParametro))
        {
            _cache.Set(MatrizCacheKey, ClonarMatriz(defaults), MatrizCacheTtl);
            return defaults;
        }

        try
        {
            var matriz = DeserializarMatriz(valorParametro);

            if (matriz.Count == 0)
            {
                _cache.Set(MatrizCacheKey, ClonarMatriz(defaults), MatrizCacheTtl);
                return defaults;
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
                x => x.Value
                    .Where(p => PermisoCanonical.ContainsKey(p))
                    .Select(p => PermisoCanonical[p])
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
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
            },
            ["FUNCIONARIO"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppPermissions.DocumentosVer
            }
        };
    }

    private static Dictionary<string, HashSet<string>> ClonarMatriz(Dictionary<string, HashSet<string>> source)
        => source.ToDictionary(
            x => x.Key,
            x => new HashSet<string>(x.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, HashSet<string>> DeserializarMatriz(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var valorNormalizado = valor.Trim();

        if (PareceJson(valorNormalizado))
        {
            try
            {
                var rawJson = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(valorNormalizado);
                if (rawJson is not null && rawJson.Count > 0)
                {
                    return rawJson.ToDictionary(
                        x => NormalizarRol(x.Key),
                        x => x.Value?
                            .Where(p => PermisoCanonical.ContainsKey(p.Trim()))
                            .Select(p => PermisoCanonical[p.Trim()])
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)
                            ?? [],
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                // Intentamos formato compacto para mantener compatibilidad con valores nuevos.
            }
        }

        var matriz = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segmento in valorNormalizado.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = segmento.Split(':', 2, StringSplitOptions.TrimEntries);
            if (partes.Length != 2)
            {
                continue;
            }

            var rolNormalizado = NormalizarRol(partes[0]);
            var permisos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var permisoRaw in partes[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (PermisoCanonical.TryGetValue(permisoRaw, out var permiso))
                {
                    permisos.Add(permiso);
                    continue;
                }

                if (int.TryParse(permisoRaw, out var indicePermiso)
                    && LegacyIndiceAPermiso.TryGetValue(indicePermiso, out var permisoLegacy))
                {
                    permisos.Add(permisoLegacy);
                }
            }

            matriz[rolNormalizado] = permisos;
        }

        return matriz;
    }

    private static bool PareceJson(string valor)
        => valor.Length > 0 && (valor[0] == '{' || valor[0] == '[');
}
