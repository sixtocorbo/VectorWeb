using System.Text.Json;
using VectorWeb.Models;
using VectorWeb.Repositories;

namespace VectorWeb.Services.Security;

public class RolePermissionService
{
    private const string ParametroPermisos = "SEGURIDAD_PERMISOS_POR_ROL";
    private readonly IRepository<CfgSistemaParametro> _repoParametros;

    public RolePermissionService(IRepository<CfgSistemaParametro> repoParametros)
    {
        _repoParametros = repoParametros;
    }

    public async Task<Dictionary<string, HashSet<string>>> ObtenerMatrizAsync()
    {
        var defaults = ObtenerMatrizDefault();
        var parametro = (await _repoParametros.FindAsync(p => p.Clave == ParametroPermisos)).FirstOrDefault();

        if (parametro is null || string.IsNullOrWhiteSpace(parametro.Valor))
        {
            return defaults;
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parametro.Valor);
            if (raw is null || raw.Count == 0)
            {
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
                if (!matriz.ContainsKey(rolDefault))
                {
                    matriz[rolDefault] = new HashSet<string>(permisosDefault, StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                if (matriz[rolDefault].Count == 0)
                {
                    matriz[rolDefault] = new HashSet<string>(permisosDefault, StringComparer.OrdinalIgnoreCase);
                }
            }

            foreach (var rol in matriz.Keys.ToList())
            {
                if (EsRolAdministrador(rol) && matriz[rol].Count == 0)
                {
                    matriz[rol] = AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }

            return matriz;
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    public async Task<HashSet<string>> ObtenerPermisosPorRolAsync(string? rol)
    {
        var matriz = await ObtenerMatrizAsync();
        var rolNormalizado = NormalizarRol(rol);

        if (matriz.TryGetValue(rolNormalizado, out var permisos))
        {
            return permisos;
        }

        if (EsRolAdministrador(rolNormalizado))
        {
            return AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return matriz["OPERADOR"];
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

    public static HashSet<string> ObtenerPermisosDefaultPorRol(string? rol)
    {
        var rolNormalizado = NormalizarRol(rol);

        if (EsRolAdministrador(rolNormalizado))
        {
            return AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.DocumentosVer,
            AppPermissions.DocumentosEditar,
            AppPermissions.VinculacionGestionar,
            AppPermissions.ReclusosGestionar,
            AppPermissions.RenovacionesGestionar
        };
    }

    public static Dictionary<string, HashSet<string>> ObtenerMatrizDefault()
    {
        return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = ObtenerPermisosDefaultPorRol("ADMIN"),
            ["ADMINISTRADOR"] = ObtenerPermisosDefaultPorRol("ADMINISTRADOR"),
            ["OPERADOR"] = ObtenerPermisosDefaultPorRol("OPERADOR")
        };
    }

    private static bool EsRolAdministrador(string? rol)
        => !string.IsNullOrWhiteSpace(rol) && rol.Contains("ADMIN", StringComparison.OrdinalIgnoreCase);

    private static string NormalizarRol(string? rol)
        => string.IsNullOrWhiteSpace(rol) ? "OPERADOR" : rol.Trim().ToUpperInvariant();
}
