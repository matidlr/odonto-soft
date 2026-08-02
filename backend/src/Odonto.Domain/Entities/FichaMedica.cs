namespace Odonto.Domain.Entities;

/// <summary>
/// Ficha médica general del paciente: una sola fila por paciente, que se
/// edita (no tiene historial de versiones — para eso están las
/// NotaEvolucion). Campos libres a propósito, porque cada clínica registra
/// esta información con distinto nivel de detalle.
/// </summary>
public class FichaMedica
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public string? Alergias { get; set; }
    public string? EnfermedadesPreexistentes { get; set; }
    public string? MedicacionActual { get; set; }
    public string? Habitos { get; set; }
    public string? Observaciones { get; set; }

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
