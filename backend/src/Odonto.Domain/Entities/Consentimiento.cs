using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Consentimiento informado de un paciente (general, cirugía, implante,
/// etc.), con el texto que se le mostró y su firma manuscrita capturada
/// digitalmente (PNG en base64, dibujada en un canvas). Se puede crear sin
/// firmar todavía (borrador) y firmar después.
/// </summary>
public class Consentimiento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public Guid? OdontologoId { get; set; }
    public Odontologo? Odontologo { get; set; }

    public TipoConsentimiento Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    public string? FirmaBase64 { get; set; }
    public string? FirmaNombreAclaratorio { get; set; }
    public DateTime? FechaFirma { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
