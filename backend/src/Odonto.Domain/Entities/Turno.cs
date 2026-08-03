using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

public class Turno
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid OdontologoId { get; set; }
    public Odontologo Odontologo { get; set; } = null!;

    // Sede donde es el turno. Nullable por compatibilidad con datos viejos
    // (se completa con la sede Principal en la migración). El chequeo de
    // solapamiento de horario del odontólogo NUNCA se filtra por sede — es
    // a propósito, para que jamás quede agendado a la misma hora en dos
    // sedes distintas.
    public Guid? SedeId { get; set; }
    public Sede? Sede { get; set; }

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public Guid? TipoTratamientoId { get; set; }
    public TipoTratamiento? TipoTratamiento { get; set; }

    public DateTime FechaHora { get; set; }
    public int DuracionMinutos { get; set; }
    public TurnoEstado Estado { get; set; } = TurnoEstado.Solicitado;
    public string? Notas { get; set; }

    // Se usa para decidir si corresponde el recordatorio de 24hs
    // (solo si se reservó con más de una semana de anticipación).
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
