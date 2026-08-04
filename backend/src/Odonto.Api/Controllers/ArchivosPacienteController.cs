using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
using Odonto.Application.Common.Interfaces;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Archivos generales del paciente (radiografías, fotos, PDFs, estudios),
/// organizados por paciente pero sin atarse a un diente puntual del
/// odontograma — esa es la diferencia con ArchivoOdontograma/OdontogramaController.
/// </summary>
[ApiController]
[Route("api/pacientes/{pacienteId}/archivos")]
[Authorize(Policy = "TenantActivo")]
public class ArchivosPacienteController : ControllerBase
{
    private const long TamanioMaximoBytes = 20 * 1024 * 1024; // 20 MB

    // Igual que en el odontograma: el ContentType que se guarda y se
    // devuelve sale de este mapa (no del header que manda el cliente, que
    // es fácil de falsificar).
    private static readonly Dictionary<string, string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ArchivosPacienteController> _logger;
    private readonly IArchivoCifrado _cifrado;

    public ArchivosPacienteController(AppDbContext db, IWebHostEnvironment env, ILogger<ArchivosPacienteController> logger, IArchivoCifrado cifrado)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _cifrado = cifrado;
    }

    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public record ArchivoPacienteResponse(
        Guid Id,
        CategoriaArchivo Categoria,
        string? Descripcion,
        string NombreOriginal,
        string ContentType,
        long TamanioBytes,
        DateTime FechaSubida);

    [HttpGet]
    public async Task<IActionResult> GetArchivos(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var archivos = await _db.ArchivosPaciente
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.FechaSubida)
            .Select(a => new ArchivoPacienteResponse(
                a.Id, a.Categoria, a.Descripcion, a.NombreOriginal, a.ContentType, a.TamanioBytes, a.FechaSubida))
            .ToListAsync(ct);

        return Ok(archivos);
    }

    [HttpPost]
    [RequestSizeLimit(TamanioMaximoBytes)]
    public async Task<IActionResult> SubirArchivo(
        Guid pacienteId,
        IFormFile archivo,
        [FromForm] CategoriaArchivo categoria,
        [FromForm] string? descripcion,
        CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "No se recibió ningún archivo." });

        if (archivo.Length > TamanioMaximoBytes)
            return BadRequest(new { message = "El archivo supera el tamaño máximo permitido (20 MB)." });

        if (!Validaciones.EsEnumValido(categoria))
            return BadRequest(new { message = "Categoría inválida." });

        if (descripcion?.Length > 500)
            return BadRequest(new { message = "La descripción es demasiado larga." });

        var extension = Path.GetExtension(archivo.FileName);
        if (!TiposPermitidos.TryGetValue(extension, out var contentTypeSeguro))
        {
            return BadRequest(new
            {
                message = "Tipo de archivo no permitido. Solo se aceptan imágenes (JPG, PNG, WEBP) y PDF."
            });
        }

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        // uploads/{tenantId}/{pacienteId}/archivos-paciente/{guid}{extension}
        // NOTA: igual que en el odontograma, esto guarda en disco local — el
        // día que se dockerice/despliegue tiene que migrar a storage externo.
        // El contenido se cifra (AES-256-GCM) antes de tocar el disco: ver
        // ArchivoCifradoService, checklist de seguridad ítem "Cifrado".
        var carpeta = Path.Combine(_env.ContentRootPath, "uploads", tenantId.ToString(), pacienteId.ToString(), "archivos-paciente");
        Directory.CreateDirectory(carpeta);

        var nombreEnDisco = $"{Guid.NewGuid()}{extension}";
        var rutaCompleta = Path.Combine(carpeta, nombreEnDisco);

        using (var streamMemoria = new MemoryStream())
        {
            await archivo.CopyToAsync(streamMemoria, ct);
            await _cifrado.CifrarAArchivoAsync(streamMemoria.ToArray(), rutaCompleta, ct);
        }

        var registro = new ArchivoPaciente
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            Categoria = categoria,
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion,
            NombreOriginal = archivo.FileName,
            RutaEnDisco = rutaCompleta,
            ContentType = contentTypeSeguro,
            TamanioBytes = archivo.Length
        };

        _db.ArchivosPaciente.Add(registro);
        await _db.SaveChangesAsync(ct);

        return Ok(new ArchivoPacienteResponse(
            registro.Id, registro.Categoria, registro.Descripcion, registro.NombreOriginal,
            registro.ContentType, registro.TamanioBytes, registro.FechaSubida));
    }

    /// <summary>Descarga un archivo. Requiere sesión (no son URLs públicas).</summary>
    [HttpGet("{archivoId}")]
    public async Task<IActionResult> DescargarArchivo(Guid pacienteId, Guid archivoId, CancellationToken ct)
    {
        var archivo = await _db.ArchivosPaciente.FirstOrDefaultAsync(a => a.Id == archivoId && a.PacienteId == pacienteId, ct);
        if (archivo is null) return NotFound();

        if (!System.IO.File.Exists(archivo.RutaEnDisco))
            return NotFound(new { message = "El archivo ya no está disponible en el servidor." });

        var contenido = await _cifrado.DescifrarDeArchivoAsync(archivo.RutaEnDisco, ct);
        return File(contenido, archivo.ContentType, archivo.NombreOriginal);
    }

    [HttpDelete("{archivoId}")]
    public async Task<IActionResult> BorrarArchivo(Guid pacienteId, Guid archivoId, CancellationToken ct)
    {
        var archivo = await _db.ArchivosPaciente.FirstOrDefaultAsync(a => a.Id == archivoId && a.PacienteId == pacienteId, ct);
        if (archivo is null) return NotFound();

        // Baja lógica: ni el registro ni el archivo físico en disco se
        // borran de verdad, para no perder la radiografía/estudio por
        // accidente. Solo se oculta de las consultas normales.
        archivo.IsDeleted = true;
        archivo.DeletedAt = DateTime.UtcNow;
        archivo.DeletedBy = UsuarioIdActual();
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Archivo {ArchivoId} del paciente {PacienteId} eliminado (baja lógica) por usuario {UsuarioId}",
            archivo.Id, pacienteId, UsuarioIdActual());

        return Ok(new { message = "Archivo eliminado." });
    }
}
