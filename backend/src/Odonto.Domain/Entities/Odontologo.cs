namespace Odonto.Domain.Entities;

public class Odontologo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    // Login por odontólogo es opcional: solo el primer odontólogo, creado
    // junto con el Owner al registrar la clínica, tiene uno. Los que se
    // agregan después son solo perfiles de datos (para el selector de
    // "qué odontólogo estoy usando"), sin usuario/contraseña propios.
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Nombre propio (ya no se deriva de Usuario.Nombre, porque puede no
    // tener un Usuario asociado).
    public string Nombre { get; set; } = string.Empty;

    public string Matricula { get; set; } = string.Empty;
    public string? Especialidad { get; set; }

    // Color usado para diferenciarlo en la agenda compartida del tenant.
    public string ColorAgenda { get; set; } = "#2563eb";
}
