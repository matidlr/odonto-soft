namespace Odonto.Application.Agenda;

public record Ventana(TimeSpan Inicio, TimeSpan Fin);

/// <summary>
/// Lógica pura para calcular los horarios realmente reservables de un día:
/// (ventanas habilitadas) menos (bloqueos) menos (turnos ya ocupados),
/// discretizado en slots que alcancen la duración del tratamiento pedido.
/// Sin dependencias de base de datos para que sea fácil de testear.
/// </summary>
public static class DisponibilidadCalculator
{
    public static List<TimeSpan> CalcularSlotsDisponibles(
        List<Ventana> ventanasHabilitadas,
        List<Ventana> bloqueos,
        List<Ventana> turnosOcupados,
        int duracionMinutos,
        int pasoMinutos = 15)
    {
        var libres = Restar(ventanasHabilitadas, bloqueos);
        libres = Restar(libres, turnosOcupados);

        var slots = new List<TimeSpan>();
        var duracion = TimeSpan.FromMinutes(duracionMinutos);
        var paso = TimeSpan.FromMinutes(pasoMinutos);

        foreach (var ventana in libres.OrderBy(v => v.Inicio))
        {
            var cursor = ventana.Inicio;
            while (cursor + duracion <= ventana.Fin)
            {
                slots.Add(cursor);
                cursor += paso;
            }
        }

        return slots;
    }

    private static List<Ventana> Restar(List<Ventana> baseVentanas, List<Ventana> aRestar)
    {
        var resultado = new List<Ventana>(baseVentanas);

        foreach (var resta in aRestar)
        {
            var siguiente = new List<Ventana>();
            foreach (var v in resultado)
            {
                // Sin solapamiento: la ventana queda intacta.
                if (resta.Fin <= v.Inicio || resta.Inicio >= v.Fin)
                {
                    siguiente.Add(v);
                    continue;
                }

                // Pedazo que queda antes del bloqueo.
                if (resta.Inicio > v.Inicio)
                {
                    siguiente.Add(new Ventana(v.Inicio, resta.Inicio));
                }

                // Pedazo que queda después del bloqueo.
                if (resta.Fin < v.Fin)
                {
                    siguiente.Add(new Ventana(resta.Fin, v.Fin));
                }
            }
            resultado = siguiente;
        }

        return resultado;
    }
}
