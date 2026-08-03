using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Presupuesto de tratamiento para un paciente: una lista de ítems (cada uno
/// con su precio) que el paciente aprueba o rechaza. Un presupuesto Aprobado
/// se puede "convertir" una sola vez: los ítems que apuntan a un diente
/// puntual generan un EventoOdontograma Planificado, para que el plan de
/// tratamiento quede reflejado en el odontograma.
/// </summary>
public class Presupuesto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public Guid? OdontologoId { get; set; }
    public Odontologo? Odontologo { get; set; }

    public EstadoPresupuesto Estado { get; set; } = EstadoPresupuesto.Pendiente;

    public string? Observaciones { get; set; }

    public bool Convertido { get; set; }
    public DateTime? FechaConversion { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRespuesta { get; set; }

    public List<ItemPresupuesto> Items { get; set; } = new();

    // Borrado lógico: "eliminar" marca estas 3 columnas en vez de borrar la
    // fila (solo se permite sobre presupuestos Pendientes, ver controller).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
