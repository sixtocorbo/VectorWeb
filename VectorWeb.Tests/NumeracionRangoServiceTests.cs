using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using VectorWeb.Models;
using VectorWeb.Services;

namespace VectorWeb.Tests;

public class NumeracionRangoServiceTests
{
    [Fact]
    public async Task GuardarRangoAsync_Falla_SiNoExisteCupo()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeNumeracionRangos.Add(new MaeNumeracionRango
            {
                IdTipo = 1,
                Anio = 2026,
                NombreRango = "EXISTENTE",
                NumeroInicio = 1,
                NumeroFin = 10,
                UltimoUtilizado = 0,
                Activo = true,
                IdOficina = 1
            });
            await seed.SaveChangesAsync();
        }

        var resultado = await service.GuardarRangoAsync(new MaeNumeracionRango
        {
            IdTipo = 1,
            Anio = 2026,
            NombreRango = "NUEVO",
            NumeroInicio = 11,
            NumeroFin = 20,
            UltimoUtilizado = 0,
            Activo = true,
            IdOficina = 1
        });

        Assert.False(resultado.Exitoso);
        Assert.Equal("No existe cupo configurado para este tipo y año.", resultado.Mensaje);

        await using var verify = new SecretariaDbContext(options);
        Assert.Single(await verify.MaeNumeracionRangos.ToListAsync());
    }

    [Fact]
    public async Task GuardarRangoAsync_Falla_SiSuperaCupoDisponible()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 15,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });

            seed.MaeNumeracionRangos.Add(new MaeNumeracionRango
            {
                IdTipo = 1,
                Anio = 2026,
                NombreRango = "EXISTENTE",
                NumeroInicio = 1,
                NumeroFin = 10,
                UltimoUtilizado = 0,
                Activo = true,
                IdOficina = 1
            });

            await seed.SaveChangesAsync();
        }

        var resultado = await service.GuardarRangoAsync(new MaeNumeracionRango
        {
            IdTipo = 1,
            Anio = 2026,
            NombreRango = "NUEVO",
            NumeroInicio = 11,
            NumeroFin = 20,
            UltimoUtilizado = 0,
            Activo = true,
            IdOficina = 1
        });

        Assert.False(resultado.Exitoso);
        Assert.Equal("El rango supera el cupo disponible para este tipo y año.", resultado.Mensaje);
    }

    private static NumeracionRangoService CrearServicio(out DbContextOptions<SecretariaDbContext> options)
    {
        options = new DbContextOptionsBuilder<SecretariaDbContext>()
            .UseInMemoryDatabase($"rangos-{Guid.NewGuid()}")
            .Options;

        return new NumeracionRangoService(new TestDbContextFactory(options), NullLogger<NumeracionRangoService>.Instance);
    }

    private sealed class TestDbContextFactory(DbContextOptions<SecretariaDbContext> options) : IDbContextFactory<SecretariaDbContext>
    {
        public SecretariaDbContext CreateDbContext() => new(options);

        public Task<SecretariaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SecretariaDbContext(options));
    }
}
