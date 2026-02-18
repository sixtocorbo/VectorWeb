using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using VectorWeb.Models;
using VectorWeb.Services.Security;

namespace VectorWeb.Tests;

public class RolePermissionServiceTests
{
    [Fact]
    public void SerializarMatriz_OrdenaPermisosPorIndiceAlfabetico()
    {
        var service = CrearServicio();
        var permisosOrdenados = AppPermissions.Todos
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permisoIndiceBajo = permisosOrdenados[0];
        var permisoIndiceAlto = permisosOrdenados[^1];

        var matriz = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Operador"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                permisoIndiceAlto,
                permisoIndiceBajo
            }
        };

        var serializado = service.SerializarMatriz(matriz);

        var indiceAlto = permisosOrdenados.Length - 1;
        Assert.Equal($"Operador:0,{indiceAlto}", serializado);
    }

    [Fact]
    public async Task ObtenerMatrizAsync_DeserializaIndicesCompactosAValoresCanonicos()
    {
        var dbName = $"role-perms-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<SecretariaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var permisosOrdenados = AppPermissions.Todos
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var indiceAlto = permisosOrdenados.Length - 1;
        var permisoIndiceBajo = permisosOrdenados[0];
        var permisoIndiceAlto = permisosOrdenados[indiceAlto];

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.CfgSistemaParametros.Add(new CfgSistemaParametro
            {
                Clave = "SEGURIDAD_PERMISOS_POR_ROL",
                Valor = $"operador:{indiceAlto},0"
            });
            await seed.SaveChangesAsync();
        }

        var service = CrearServicio(new TestDbContextFactory(options));
        var matriz = await service.ObtenerMatrizAsync(forzarRefresco: true);

        Assert.True(matriz.TryGetValue("OPERADOR", out var permisosOperador));
        Assert.NotNull(permisosOperador);
        Assert.Equal(2, permisosOperador!.Count);
        Assert.Contains(permisoIndiceBajo, permisosOperador);
        Assert.Contains(permisoIndiceAlto, permisosOperador);
    }

    private static RolePermissionService CrearServicio(IDbContextFactory<SecretariaDbContext>? dbFactory = null)
    {
        dbFactory ??= new TestDbContextFactory(
            new DbContextOptionsBuilder<SecretariaDbContext>()
                .UseInMemoryDatabase($"role-perms-empty-{Guid.NewGuid()}")
                .Options);

        return new RolePermissionService(dbFactory, new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class TestDbContextFactory(DbContextOptions<SecretariaDbContext> options) : IDbContextFactory<SecretariaDbContext>
    {
        public SecretariaDbContext CreateDbContext() => new(options);

        public Task<SecretariaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SecretariaDbContext(options));
    }
}
