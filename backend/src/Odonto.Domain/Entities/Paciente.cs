namespace Odonto.Domain.Entities;

public class Paciente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid? OdontologoPrincipalId { get; set; }
    public Odontologo? OdontologoPrincipal { get; set; }

    public string Nombre { get; set; } = string.Empty;
    // Opcional a propósito: los pacientes cargados antes de este campo
    // existir quedan con Apellido null (no se puede rellenar solo). Se
    // usa para agrupar/ordenar la lista alfabéticamente; si está vacío,
    // el paciente se agrupa por la inicial de Nombre en su lugar.
    public string? Apellido { get; set; }
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Baja lógica: un paciente "eliminado" queda con Activo=false pero sus
    // datos, turnos e historia clínica se conservan (nunca se borran de
    // verdad). Se puede reactivar.
    public bool Activo { get; set; } = true;
}
