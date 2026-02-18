namespace VectorWeb.Utils;

public static class FechaHelper
{
    private static readonly HashSet<(int Mes, int Dia)> FeriadosFijosUy =
    [
        (1, 1),   // Año Nuevo
        (1, 6),   // Reyes
        (4, 19),  // Desembarco de los 33 Orientales
        (5, 1),   // Día de los Trabajadores
        (6, 18),  // Natalicio de Artigas
        (7, 18),  // Jura de la Constitución
        (8, 25),  // Declaratoria de la Independencia
        (11, 2),  // Día de los Difuntos
        (12, 25)  // Navidad
    ];

    private static readonly Dictionary<int, HashSet<DateOnly>> FeriadosMovilesPorAnio = [];

    public static DateTime AgregarDiasHabiles(this DateTime fechaInicio, int dias, ISet<DateOnly>? feriados = null)
    {
        if (dias <= 0)
        {
            return AjustarFinDia(fechaInicio);
        }

        var fecha = fechaInicio;
        var diasAgregados = 0;

        while (diasAgregados < dias)
        {
            fecha = fecha.AddDays(1);

            if (!EsDiaHabil(fecha, feriados))
            {
                continue;
            }

            diasAgregados++;
        }

        return AjustarFinDia(fecha);
    }

    public static DateTime AgregarDiasCorridos(this DateTime fechaInicio, int dias)
    {
        return AjustarFinDia(fechaInicio.AddDays(dias));
    }

    public static bool EsDiaHabil(this DateTime fecha, ISet<DateOnly>? feriados = null)
    {
        if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var fechaOnly = DateOnly.FromDateTime(fecha);

        return !EsFeriado(fechaOnly, feriados);
    }

    private static DateTime AjustarFinDia(DateTime fecha)
    {
        return fecha.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
    }

    private static bool EsFeriado(DateOnly fecha, ISet<DateOnly>? feriados)
    {
        if (feriados is not null && feriados.Contains(fecha))
        {
            return true;
        }

        if (FeriadosFijosUy.Contains((fecha.Month, fecha.Day)))
        {
            return true;
        }

        return ObtenerFeriadosMoviles(fecha.Year).Contains(fecha);
    }

    private static HashSet<DateOnly> ObtenerFeriadosMoviles(int anio)
    {
        if (FeriadosMovilesPorAnio.TryGetValue(anio, out var feriados))
        {
            return feriados;
        }

        var domingoPascua = CalcularDomingoPascua(anio);
        var nuevosFeriados = new HashSet<DateOnly>
        {
            domingoPascua.AddDays(-48), // Carnaval (lunes)
            domingoPascua.AddDays(-47), // Carnaval (martes)
            domingoPascua.AddDays(-7),  // Semana de Turismo (lunes)
            domingoPascua.AddDays(-6),  // Semana de Turismo (martes)
            domingoPascua.AddDays(-5),  // Semana de Turismo (miércoles)
            domingoPascua.AddDays(-4),  // Semana de Turismo (jueves)
            domingoPascua.AddDays(-2)   // Viernes Santo
        };

        FeriadosMovilesPorAnio[anio] = nuevosFeriados;
        return nuevosFeriados;
    }

    private static DateOnly CalcularDomingoPascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateOnly(anio, mes, dia);
    }
}
