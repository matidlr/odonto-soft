namespace Odonto.Domain.Entities;

/// <summary>
/// Un lugar físico donde atiende un odontólogo (su consultorio propio, u
/// otra clínica en la que también trabaja). Mismo login, mismos pacientes y
/// tratamientos de siempre — lo que cambia por sede es la dirección y el
/// horario de atención (Disponibilidad). Cada odontólogo arranca con una
/// sede "Principal" automática (no se puede borrar); puede agregar más.
/// Los turnos quedan atados a una sede, pero NUNCA se permite superponer
/// horarios del mismo odontólogo entre sedes distintas (ver TurnosController).
/// </summary>
public class Sede
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid OdontologoId { get; set; }
    public Odontologo Odontologo { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }

    public bool EsPrincipal { get; set; }
    public bool Activa { get; set; } = true;
}
