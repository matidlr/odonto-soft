using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Un evento en la historia clínica dental de un diente puntual: "este día,
/// este diente pasó a tener este estado, por este tratamiento". El estado
/// ACTUAL de un diente no se guarda aparte: es el evento más reciente para
/// ese NumeroFdi. Si un diente nunca tuvo eventos, se asume Sano.
/// </summary>
public class EventoOdontograma
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    // Numeración FDI de piezas permanentes: cuadrantes 1-4, posiciones 1-8
    // dentro de cada cuadrante (11-18, 21-28, 31-38, 41-48).
    public int NumeroFdi { get; set; }

    public EstadoDiente Estado { get; set; }

    // Si el evento ya se realizó o todavía está planificado (plan de
    // tratamiento a futuro). Por defecto Realizado, para no romper el
    // caso simple de "registro lo que ya hice".
    public EstadoTratamiento EstadoTratamiento { get; set; } = EstadoTratamiento.Realizado;

    public string? Tratamiento { get; set; }
    public string? Nota { get; set; }

    public Guid? OdontologoId { get; set; }
    public Odontologo? Odontologo { get; set; }

    // Si el tratamiento se hizo durante un turno reservado por el sistema,
    // lo asociamos acá y la Fecha del evento pasa a ser la FechaHora de ese
    // turno. Si no hubo turno (carga retroactiva, historia previa, etc.),
    // TurnoId queda null y Fecha se carga a mano.
    public Guid? TurnoId { get; set; }
    public Turno? Turno { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
