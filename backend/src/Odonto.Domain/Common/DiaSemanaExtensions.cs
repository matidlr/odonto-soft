using System;

namespace Odonto.Domain.Common;

public static class DiaSemanaExtensions
{
    public static DiaSemana ADiaSemana(this DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => DiaSemana.Lunes,
        DayOfWeek.Tuesday => DiaSemana.Martes,
        DayOfWeek.Wednesday => DiaSemana.Miercoles,
        DayOfWeek.Thursday => DiaSemana.Jueves,
        DayOfWeek.Friday => DiaSemana.Viernes,
        DayOfWeek.Saturday => DiaSemana.Sabado,
        DayOfWeek.Sunday => DiaSemana.Domingo,
        _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
    };
}
