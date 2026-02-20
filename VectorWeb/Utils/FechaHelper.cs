using System;
using System.Collections.Generic;

namespace VectorWeb.Utils;

public static class FechaHelper
{
    // Lista OFICIAL de feriados inamovibles Uruguay
    private static readonly HashSet<(int Mes, int Dia)> FeriadosFijosUy =
    [
        (1, 1),   // Año Nuevo
        (1, 6),   // Reyes
        (5, 1),   // Día de los Trabajadores
        (6, 19),  // Natalicio de Artigas
        (7, 18),  // Jura de la Constitución
        (8, 25),  // Declaratoria de la Independencia
        (11, 2),  // Difuntos
        (12, 25)  // Navidad
    ];

    // Feriados laborables trasladables por ley
    private static readonly (int Mes, int Dia)[] FeriadosTrasladablesUy =
    [
        (4, 19),  // Desembarco de los 33
        (5, 18),  // Batalla de las Piedras
        (10, 12)  // Día de la Raza
    ];

    private static readonly Dictionary<int, HashSet<DateOnly>> FeriadosMovilesPorAnio = [];

    public static DateTime AgregarDiasHabiles(this DateTime fechaInicio, int dias, ISet<DateOnly>? feriados = null)
    {
        if (dias <= 0) return AjustarFinDia(fechaInicio);

        var fecha = fechaInicio;
        var diasAgregados = 0;

        while (diasAgregados < dias)
        {
            fecha = fecha.AddDays(1); // Avanzamos al siguiente día

            // Si es Sábado, Domingo o Feriado, NO sumamos al contador (continue)
            if (!EsDiaHabil(fecha, feriados))
            {
                continue;
            }

            // Si es día válido, sumamos 1 al contador de días cumplidos
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
        // 1. Fin de semana
        if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var fechaOnly = DateOnly.FromDateTime(fecha);

        // 2. Feriados (Fijos o Móviles)
        return !EsFeriado(fechaOnly, feriados);
    }

    private static DateTime AjustarFinDia(DateTime fecha)
    {
        // Retorna la fecha a las 23:59:59 para que el vencimiento cubra todo el día
        return fecha.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
    }

    private static bool EsFeriado(DateOnly fecha, ISet<DateOnly>? feriados)
    {
        // Feriados personalizados pasados por parámetro (opcional)
        if (feriados is not null && feriados.Contains(fecha)) return true;

        // Feriados Fijos
        if (FeriadosFijosUy.Contains((fecha.Month, fecha.Day))) return true;

        // Feriados Móviles (Semana de Turismo, Carnaval y trasladables)
        return ObtenerFeriadosMoviles(fecha.Year).Contains(fecha);
    }

    private static HashSet<DateOnly> ObtenerFeriadosMoviles(int anio)
    {
        if (FeriadosMovilesPorAnio.TryGetValue(anio, out var feriados))
        {
            return feriados;
        }

        var domingoPascua = CalcularDomingoPascua(anio);

        // Cálculo correcto de Carnaval y Turismo
        var nuevosFeriados = new HashSet<DateOnly>
        {
            domingoPascua.AddDays(-48), // Lunes Carnaval
            domingoPascua.AddDays(-47), // Martes Carnaval
            domingoPascua.AddDays(-6),  // Lunes Turismo
            domingoPascua.AddDays(-5),  // Martes Turismo
            domingoPascua.AddDays(-4),  // Miércoles Turismo
            domingoPascua.AddDays(-3),  // Jueves Santo
            domingoPascua.AddDays(-2)   // Viernes Santo
        };

        foreach (var (mes, dia) in FeriadosTrasladablesUy)
        {
            nuevosFeriados.Add(CalcularFeriadoTrasladado(anio, mes, dia));
        }

        FeriadosMovilesPorAnio[anio] = nuevosFeriados;
        return nuevosFeriados;
    }

    private static DateOnly CalcularFeriadoTrasladado(int anio, int mes, int dia)
    {
        var fechaOriginal = new DateOnly(anio, mes, dia);

        return fechaOriginal.DayOfWeek switch
        {
            DayOfWeek.Tuesday => fechaOriginal.AddDays(-1),
            DayOfWeek.Wednesday => fechaOriginal.AddDays(-2),
            DayOfWeek.Thursday => fechaOriginal.AddDays(4),
            DayOfWeek.Friday => fechaOriginal.AddDays(3),
            _ => fechaOriginal
        };
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
