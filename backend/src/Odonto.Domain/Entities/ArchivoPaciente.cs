using Odonto.Domain.Common;

namespace Odonto.Domain.Entities;

/// <summary>
/// Archivo general del paciente (radiografía, foto, PDF, estudio) que no
/// está atado a un diente puntual del odontograma — es la carpeta general
/// de documentos de esa persona. El archivo en sí se guarda en disco (por
/// ahora local; el día que esto se dockerice/despliegue hay que migrar a
/// un storage externo tipo S3/Blob), acá solo guardamos la referencia.
/// </summary>
public class ArchivoPaciente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public CategoriaArchivo Categoria { get; set; } = CategoriaArchivo.Documento;
    public string? Descripcion { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaEnDisco { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }

    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

    // Borrado lógico: "eliminar" marca estas 3 columnas en vez de borrar la
    // fila. El archivo físico en disco también se conserva (ver
    // ArchivosPacienteController) para no perder la radiografía/estudio.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
