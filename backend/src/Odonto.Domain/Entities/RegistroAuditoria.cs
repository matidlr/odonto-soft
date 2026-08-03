namespace Odonto.Domain.Entities;

/// <summary>
/// Auditoría de cambios en la historia clínica (odontograma, ficha médica,
/// notas de evolución): quién, cuándo, qué acción y, cuando corresponde,
/// el valor anterior y el nuevo. A diferencia de los logs de la app (que
/// nunca llevan datos médicos), esto vive en la misma base protegida que
/// el resto de la historia clínica — es justamente el lugar pensado para
/// guardar ese detalle, con el mismo aislamiento por tenant.
/// </summary>
public class RegistroAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    // Puede ser null si en algún momento se registra algo desde un proceso
    // sin usuario logueado (no debería pasar hoy, pero no lo forzamos).
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    // Nombre de la entidad afectada: "EventoOdontograma", "FichaMedica", "NotaEvolucion".
    public string Entidad { get; set; } = string.Empty;
    public Guid EntidadId { get; set; }

    // "Creado" / "Editado".
    public string Accion { get; set; } = string.Empty;

    // Campo puntual que cambió (por ej. "Alergias", "Estado"). Null cuando
    // la acción es sobre el registro entero (por ej. crear una nota).
    public string? Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
}
