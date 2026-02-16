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
                if (string.IsNullOrWhiteSpace(rol))
                {
                    continue;
                }

                var permisosValidos = permisos?
                    .Where(p => AppPermissions.Todos.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .Select(p => p.Trim().ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? [];

                matriz[rol.Trim().ToUpperInvariant()] = permisosValidos;
            }

            foreach (var (rolDefault, permisosDefault) in defaults)
            {
                if (!matriz.ContainsKey(rolDefault))
                {
                    matriz[rolDefault] = permisosDefault;
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
        var rolNormalizado = string.IsNullOrWhiteSpace(rol) ? "OPERADOR" : rol.Trim().ToUpperInvariant();

        if (matriz.TryGetValue(rolNormalizado, out var permisos))
        {
            return permisos;
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

    public static Dictionary<string, HashSet<string>> ObtenerMatrizDefault()
    {
        return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = AppPermissions.Todos.ToHashSet(StringComparer.OrdinalIgnoreCase),
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
}
