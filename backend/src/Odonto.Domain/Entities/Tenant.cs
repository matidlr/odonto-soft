using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;

    // Usado en el link público de registro de pacientes: /r/{Slug}
    public string Slug { get; set; } = string.Empty;

    public TenantEstado Estado { get; set; } = TenantEstado.PendienteDeActivacion;

    // Referencia a la suscripción de Mercado Pago asociada a este tenant.
    public string? MercadoPagoPreapprovalId { get; set; }
    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }

    // Mes gratis: mientras hoy sea antes de esta fecha, el tenant tiene
    // acceso aunque no haya pagado todavía. Se calcula una sola vez, al
    // activarse la clínica (registro o activación manual del SuperAdmin).
    public DateTime? FechaFinPrueba { get; set; }

    // true solo mientras Mercado Pago confirmó una suscripción "authorized"
    // vigente (lo actualiza el webhook). No tiene que ver con el período de
    // prueba: sirve para saber si, pasado el mes gratis, el tenant sigue
    // teniendo derecho a usar el sistema.
    public bool TienePagoActivo { get; set; }

    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    // Datos de contacto de la clínica, editables desde Configuración.
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? EmailContacto { get; set; }

    // Logo de la clínica, guardado en disco igual que el resto de los
    // archivos subidos (ver nota en ArchivoPaciente sobre migrar a un
    // storage externo el día que esto se dockerice/despliegue).
    public string? LogoRutaEnDisco { get; set; }
    public string? LogoContentType { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
}
