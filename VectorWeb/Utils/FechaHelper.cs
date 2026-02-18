namespace VectorWeb.Utils;

public static class FechaHelper
{
    public static DateTime AgregarDiasHabiles(this DateTime fechaInicio, int dias)
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

            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
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

    private static DateTime AjustarFinDia(DateTime fecha)
    {
        return fecha.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
    }
}
