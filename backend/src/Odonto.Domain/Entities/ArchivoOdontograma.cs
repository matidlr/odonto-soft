namespace Odonto.Domain.Entities;

/// <summary>
/// Un archivo adjunto a un evento del odontograma (radiografía, foto, etc.).
/// El archivo en sí se guarda en disco (por ahora local; el día que esto se
/// dockerice/despliegue en la nube hay que mover esto a un storage externo
/// tipo S3/Blob, porque el disco del contenedor no persiste); acá solo
/// guardamos la referencia.
/// </summary>
public class ArchivoOdontograma
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid EventoOdontogramaId { get; set; }
    public EventoOdontograma EventoOdontograma { get; set; } = null!;

    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaEnDisco { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }

    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}
