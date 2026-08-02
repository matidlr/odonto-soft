namespace Odonto.Domain.Entities;

/// <summary>
/// Nota de evolución clínica general del paciente (no atada a un diente
/// puntual, a diferencia de EventoOdontograma). Forman un historial
/// cronológico de consultas: "este día, pasó esto". Igual que en el
/// odontograma, puede asociarse a un turno reservado por el sistema o
/// cargarse con fecha manual.
/// </summary>
public class NotaEvolucion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public string? Motivo { get; set; }
    public string? Diagnostico { get; set; }
    public string? TratamientoRealizado { get; set; }
    public string? Evolucion { get; set; }
    public string? Medicacion { get; set; }
    public string? Observaciones { get; set; }

    public Guid? OdontologoId { get; set; }
    public Odontologo? Odontologo { get; set; }

    public Guid? TurnoId { get; set; }
    public Turno? Turno { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
