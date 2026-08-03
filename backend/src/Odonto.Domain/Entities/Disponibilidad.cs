using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Regla de disponibilidad de un odontólogo. Dos tipos posibles:
/// - Recurrente: patrón semanal habitual (ej: todos los lunes de 9 a 13).
/// - Excepcion: puntual sobre una fecha concreta. Puede bloquear
///   (Bloqueado=true, ej. vacaciones o un día que no atiende) o abrir un
///   hueco extra fuera del patrón habitual (Bloqueado=false, ej. un sábado
///   especial que sí atiende).
/// Los horarios reservables se calculan combinando estas reglas con los
/// turnos ya tomados (ver DisponibilidadCalculator).
/// </summary>
public class Disponibilidad
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid OdontologoId { get; set; }
    public Odontologo Odontologo { get; set; } = null!;

    // Sede a la que aplica esta regla de horario. Nullable por compatibilidad
    // con datos viejos (se completa con la sede Principal en la migración);
    // los endpoints nuevos siempre la piden o la infieren.
    public Guid? SedeId { get; set; }
    public Sede? Sede { get; set; }

    public TipoDisponibilidad Tipo { get; set; }

    // Solo aplica si Tipo == Recurrente
    public DiaSemana? DiaSemana { get; set; }

    // Solo aplica si Tipo == Excepcion (se usa solo la parte de fecha)
    public DateTime? Fecha { get; set; }

    public bool TodoElDia { get; set; }
    public TimeSpan? HoraInicio { get; set; }
    public TimeSpan? HoraFin { get; set; }

    // true = este rango bloquea (no se puede reservar)
    // false = este rango habilita (regla de trabajo normal, o apertura extra)
    public bool Bloqueado { get; set; }
}
