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

        if (feriados is not null)
        {
            return !feriados.Contains(fechaOnly);
        }

        return !FeriadosFijosUy.Contains((fechaOnly.Month, fechaOnly.Day));
    }

    private static DateTime AjustarFinDia(DateTime fecha)
    {
        return fecha.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
    }
}
