using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using VectorWeb.Models;
using VectorWeb.Services;

namespace VectorWeb.Tests;

public class NumeracionRangoServiceTests
{
    [Fact]
    public async Task ObtenerRangosPorOficinaYAnioAsync_IncluyeActivosEInactivos()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeNumeracionRangos.AddRange(
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "ACTIVO",
                    NumeroInicio = 1,
                    NumeroFin = 10,
                    UltimoUtilizado = 5,
                    Activo = true,
                    IdOficina = 1
                },
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "INACTIVO",
                    NumeroInicio = 11,
                    NumeroFin = 20,
                    UltimoUtilizado = 15,
                    Activo = false,
                    IdOficina = 1
                },
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2025,
                    NombreRango = "OTRO-ANIO",
                    NumeroInicio = 21,
                    NumeroFin = 30,
                    UltimoUtilizado = 21,
                    Activo = false,
                    IdOficina = 1
                });

            await seed.SaveChangesAsync();
        }

        var rangos = await service.ObtenerRangosPorOficinaYAnioAsync(1, 1, 2026);

        Assert.Equal(2, rangos.Count);
        Assert.Contains(rangos, r => r.NombreRango == "ACTIVO");
        Assert.Contains(rangos, r => r.NombreRango == "INACTIVO");
    }

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

    [Fact]
    public async Task GuardarCupoAsync_AjustaRangos_SiSeReduceElCupo()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 200,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });

            seed.MaeNumeracionRangos.AddRange(
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "R1",
                    NumeroInicio = 1,
                    NumeroFin = 100,
                    UltimoUtilizado = 50,
                    Activo = true,
                    IdOficina = 1
                },
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "R2",
                    NumeroInicio = 101,
                    NumeroFin = 200,
                    UltimoUtilizado = 150,
                    Activo = true,
                    IdOficina = 2
                });

            await seed.SaveChangesAsync();
        }

        var resultado = await service.GuardarCupoAsync(1, 2026, 120);

        Assert.True(resultado.Exitoso);
        Assert.Contains("Se ajustaron", resultado.Mensaje);

        await using var verify = new SecretariaDbContext(options);
        var rangos = await verify.MaeNumeracionRangos.OrderBy(r => r.NumeroInicio).ToListAsync();
        Assert.Equal(2, rangos.Count);
        Assert.Equal(120, rangos[1].NumeroFin);
    }


    [Fact]
    public async Task ObtenerSugerenciaRangoAsync_SugiereHuecoMinimoQueCumpleCantidad()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 3000,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });

            seed.MaeNumeracionRangos.AddRange(
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "BANDEJA",
                    NumeroInicio = 1,
                    NumeroFin = 200,
                    UltimoUtilizado = 187,
                    Activo = true,
                    IdOficina = 10
                },
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "PERSONAL",
                    NumeroInicio = 201,
                    NumeroFin = 501,
                    UltimoUtilizado = 220,
                    Activo = true,
                    IdOficina = 20
                });

            await seed.SaveChangesAsync();
        }

        var sugerencia = await service.ObtenerSugerenciaRangoAsync(1, 2026, 10, null, 50);

        Assert.Equal(502, sugerencia.NumeroInicioSugerido);
        Assert.Equal(551, sugerencia.NumeroFinSugerido);
        Assert.Equal(50, sugerencia.CantidadSugerida);
        Assert.Equal(2499, sugerencia.MaximoConsecutivoDisponible);
        Assert.False(sugerencia.SugerenciaRecortadaPorSaldo);
    }

    [Fact]
    public async Task ObtenerSugerenciaRangoAsync_RecortaSiCantidadNoCabeEnNingunBloque()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 3000,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });

            seed.MaeNumeracionRangos.AddRange(
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "R1",
                    NumeroInicio = 201,
                    NumeroFin = 501,
                    UltimoUtilizado = 250,
                    Activo = true,
                    IdOficina = 20
                },
                new MaeNumeracionRango
                {
                    IdTipo = 1,
                    Anio = 2026,
                    NombreRango = "R2",
                    NumeroInicio = 700,
                    NumeroFin = 3000,
                    UltimoUtilizado = 1000,
                    Activo = true,
                    IdOficina = 30
                });

            await seed.SaveChangesAsync();
        }

        var sugerencia = await service.ObtenerSugerenciaRangoAsync(1, 2026, 10, null, 250);

        Assert.Equal(1, sugerencia.NumeroInicioSugerido);
        Assert.Equal(200, sugerencia.NumeroFinSugerido);
        Assert.Equal(200, sugerencia.CantidadSugerida);
        Assert.Equal(200, sugerencia.MaximoConsecutivoDisponible);
        Assert.True(sugerencia.SugerenciaRecortadaPorSaldo);
    }

    [Fact]
    public async Task EliminarCupoAsync_Falla_SiHayRangosAsociados()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 100,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });

            seed.MaeNumeracionRangos.Add(new MaeNumeracionRango
            {
                IdTipo = 1,
                Anio = 2026,
                NombreRango = "R1",
                NumeroInicio = 1,
                NumeroFin = 100,
                UltimoUtilizado = 50,
                Activo = true,
                IdOficina = 1
            });

            await seed.SaveChangesAsync();
        }

        var resultado = await service.EliminarCupoAsync(1, 2026);

        Assert.False(resultado.Exitoso);
        Assert.Contains("No se puede eliminar el cupo", resultado.Mensaje);
    }

    [Fact]
    public async Task EliminarCupoAsync_Elimina_SiNoHayRangosAsociados()
    {
        var service = CrearServicio(out var options);

        await using (var seed = new SecretariaDbContext(options))
        {
            seed.MaeCuposSecretaria.Add(new MaeCuposSecretarium
            {
                IdTipo = 1,
                Anio = 2026,
                Cantidad = 100,
                Fecha = DateTime.Now,
                NombreCupo = "CUPO-1-2026"
            });
            await seed.SaveChangesAsync();
        }

        var resultado = await service.EliminarCupoAsync(1, 2026);

        Assert.True(resultado.Exitoso);

        await using var verify = new SecretariaDbContext(options);
        Assert.Empty(await verify.MaeCuposSecretaria.ToListAsync());
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
