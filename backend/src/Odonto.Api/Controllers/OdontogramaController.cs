using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Odontograma del paciente: piezas permanentes (numeración FDI 11-48) y
/// temporales/de leche (51-85), cada una con un estado actual que se
/// deriva del evento más reciente registrado para ese diente. No existe
/// un endpoint para "editar" un estado: cada cambio queda como un evento
/// nuevo, formando el historial clínico. Cada evento puede tener archivos
/// adjuntos (radiografías, fotos).
/// </summary>
[ApiController]
[Route("api/odontograma")]
[Authorize(Policy = "TenantActivo")]
public class OdontogramaController : ControllerBase
{
    // Permanentes: cuadrantes 1-4, posiciones 1-8 (11-18, 21-28, 31-38, 41-48).
    // Temporales/de leche: cuadrantes 5-8, posiciones 1-5 (51-55, 61-65, 71-75, 81-85).
    private static readonly int[] NumerosFdiPermanentes = Enumerable.Range(1, 4)
        .SelectMany(cuadrante => Enumerable.Range(1, 8).Select(posicion => cuadrante * 10 + posicion))
        .ToArray();

    private static readonly int[] NumerosFdiTemporales = Enumerable.Range(5, 4)
        .SelectMany(cuadrante => Enumerable.Range(1, 5).Select(posicion => cuadrante * 10 + posicion))
        .ToArray();

    private static readonly int[] NumerosFdi = NumerosFdiPermanentes.Concat(NumerosFdiTemporales).ToArray();

    private const long TamanioMaximoBytes = 20 * 1024 * 1024; // 20 MB

    // Solo imágenes y PDF: son los únicos tipos que este flujo necesita
    // (radiografías, fotos intraorales, estudios). El ContentType que
    // guardamos y devolvemos sale de ESTE mapa, no del header que manda el
    // cliente (que es fácil de falsificar) — así el visor del frontend
    // nunca termina mostrando/ejecutando algo que no sea lo esperado.
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

    public OdontogramaController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public record EstadoPiezaResponse(
        int NumeroFdi,
        EstadoDiente Estado,
        EstadoTratamiento? EstadoTratamiento,
        DateTime? Fecha,
        string? Tratamiento,
        string? Nota);

    /// <summary>Estado actual de todas las piezas (Sano si nunca tuvieron eventos).</summary>
    [HttpGet("{pacienteId}")]
    public async Task<IActionResult> GetEstadoActual(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var eventos = await _db.EventosOdontograma
            .Where(e => e.PacienteId == pacienteId)
            .OrderByDescending(e => e.Fecha)
            .ToListAsync(ct);

        var ultimoPorDiente = eventos
            .GroupBy(e => e.NumeroFdi)
            .ToDictionary(g => g.Key, g => g.First());

        var resultado = NumerosFdi.Select(numero =>
            ultimoPorDiente.TryGetValue(numero, out var evento)
                ? new EstadoPiezaResponse(numero, evento.Estado, evento.EstadoTratamiento, evento.Fecha, evento.Tratamiento, evento.Nota)
                : new EstadoPiezaResponse(numero, EstadoDiente.Sano, null, null, null, null));

        return Ok(resultado);
    }

    public record ArchivoResponse(Guid Id, string NombreOriginal, string ContentType, long TamanioBytes, DateTime FechaSubida);

    public record EventoResponse(
        Guid Id,
        int NumeroFdi,
        EstadoDiente Estado,
        EstadoTratamiento EstadoTratamiento,
        string? Tratamiento,
        string? Nota,
        Guid? OdontologoId,
        Guid? TurnoId,
        DateTime Fecha,
        List<ArchivoResponse> Archivos);

    /// <summary>Historial completo (o de un diente puntual si se pasa numeroFdi).</summary>
    [HttpGet("{pacienteId}/historial")]
    public async Task<IActionResult> GetHistorial(Guid pacienteId, [FromQuery] int? numeroFdi, CancellationToken ct)
    {
        var query = _db.EventosOdontograma.Where(e => e.PacienteId == pacienteId);
        if (numeroFdi is int n) query = query.Where(e => e.NumeroFdi == n);

        var eventos = await query
            .OrderByDescending(e => e.Fecha)
            .Select(e => new
            {
                e.Id,
                e.NumeroFdi,
                e.Estado,
                e.EstadoTratamiento,
                e.Tratamiento,
                e.Nota,
                e.OdontologoId,
                e.TurnoId,
                e.Fecha
            })
            .ToListAsync(ct);

        // Traemos los archivos por evento en una segunda consulta simple
        // (EventoOdontograma no tiene una colección de navegación a sus
        // archivos, así que los juntamos acá en memoria por EventoOdontogramaId).
        var eventoIds = eventos.Select(e => e.Id).ToList();
        var archivosPorEvento = await _db.ArchivosOdontograma
            .Where(a => eventoIds.Contains(a.EventoOdontogramaId))
            .Select(a => new { a.EventoOdontogramaId, a.Id, a.NombreOriginal, a.ContentType, a.TamanioBytes, a.FechaSubida })
            .ToListAsync(ct);

        var resultado = eventos.Select(e => new EventoResponse(
            e.Id,
            e.NumeroFdi,
            e.Estado,
            e.EstadoTratamiento,
            e.Tratamiento,
            e.Nota,
            e.OdontologoId,
            e.TurnoId,
            e.Fecha,
            archivosPorEvento
                .Where(a => a.EventoOdontogramaId == e.Id)
                .Select(a => new ArchivoResponse(a.Id, a.NombreOriginal, a.ContentType, a.TamanioBytes, a.FechaSubida))
                .ToList()));

        return Ok(resultado);
    }

    public record CrearEventoRequest(
        int NumeroFdi,
        EstadoDiente Estado,
        EstadoTratamiento? EstadoTratamiento,
        string? Tratamiento,
        string? Nota,
        Guid? OdontologoId,
        // Si se pasa TurnoId, la fecha del evento es la del turno (y se
        // ignora Fecha). Si no hay turno asociado, se usa Fecha si vino, o
        // "ahora" como último recurso.
        Guid? TurnoId,
        DateTime? Fecha);

    [HttpPost("{pacienteId}/eventos")]
    public async Task<IActionResult> CrearEvento(Guid pacienteId, CrearEventoRequest request, CancellationToken ct)
    {
        if (!NumerosFdi.Contains(request.NumeroFdi))
            return BadRequest(new { message = "NumeroFdi inválido. Debe ser una pieza permanente (11-18, 21-28, 31-38, 41-48) o temporal (51-55, 61-65, 71-75, 81-85)." });

        if (!Validaciones.EsEnumValido(request.Estado))
            return BadRequest(new { message = "Estado de diente inválido." });

        if (!Validaciones.EsEnumValido(request.EstadoTratamiento))
            return BadRequest(new { message = "Estado de tratamiento inválido." });

        if (request.Tratamiento?.Length > 300)
            return BadRequest(new { message = "El texto de tratamiento es demasiado largo." });

        if (request.Nota?.Length > 1000)
            return BadRequest(new { message = "La nota es demasiado larga." });

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        if (request.OdontologoId is Guid odontologoId)
        {
            var existeOdontologo = await _db.Odontologos.AnyAsync(o => o.Id == odontologoId, ct);
            if (!existeOdontologo) return BadRequest(new { message = "Odontólogo inválido." });
        }

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        DateTime fecha;
        Guid? turnoId = null;

        if (request.TurnoId is Guid tid)
        {
            var turno = await _db.Turnos.FirstOrDefaultAsync(t => t.Id == tid && t.PacienteId == pacienteId, ct);
            if (turno is null)
                return BadRequest(new { message = "El turno indicado no existe o no pertenece a este paciente." });

            turnoId = turno.Id;
            fecha = turno.FechaHora;
        }
        else
        {
            fecha = request.Fecha ?? DateTime.UtcNow;
        }

        // El estado "anterior" de este diente es el del evento más reciente
        // ya cargado para el mismo paciente y pieza (o "Sano" si es el
        // primer evento que se le carga a ese diente).
        var eventoAnterior = await _db.EventosOdontograma
            .Where(e => e.PacienteId == pacienteId && e.NumeroFdi == request.NumeroFdi)
            .OrderByDescending(e => e.Fecha)
            .FirstOrDefaultAsync(ct);
        var estadoAnterior = eventoAnterior?.Estado ?? EstadoDiente.Sano;

        var evento = new EventoOdontograma
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            NumeroFdi = request.NumeroFdi,
            Estado = request.Estado,
            EstadoTratamiento = request.EstadoTratamiento ?? EstadoTratamiento.Realizado,
            Tratamiento = request.Tratamiento,
            Nota = request.Nota,
            OdontologoId = request.OdontologoId,
            TurnoId = turnoId,
            Fecha = fecha
        };

        _db.EventosOdontograma.Add(evento);

        var usuarioIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid.TryParse(usuarioIdClaim, out var usuarioId);

        _db.RegistrosAuditoria.Add(new RegistroAuditoria
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            UsuarioId = usuarioId,
            Entidad = "EventoOdontograma",
            EntidadId = evento.Id,
            Accion = eventoAnterior is null ? "Creado" : "Editado",
            Campo = $"Pieza {request.NumeroFdi} - Estado",
            ValorAnterior = estadoAnterior.ToString(),
            ValorNuevo = request.Estado.ToString()
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new { evento.Id, evento.Fecha, evento.TurnoId });
    }

    /// <summary>Sube un archivo (radiografía, foto) para un evento ya existente.</summary>
    [HttpPost("eventos/{eventoId}/archivos")]
    [RequestSizeLimit(TamanioMaximoBytes)]
    public async Task<IActionResult> SubirArchivo(Guid eventoId, IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "No se recibió ningún archivo." });

        if (archivo.Length > TamanioMaximoBytes)
            return BadRequest(new { message = "El archivo supera el tamaño máximo permitido (20 MB)." });

        var extension = Path.GetExtension(archivo.FileName);
        if (!TiposPermitidos.TryGetValue(extension, out var contentTypeSeguro))
        {
            return BadRequest(new
            {
                message = "Tipo de archivo no permitido. Solo se aceptan imágenes (JPG, PNG, WEBP) y PDF."
            });
        }

        var evento = await _db.EventosOdontograma.FirstOrDefaultAsync(e => e.Id == eventoId, ct);
        if (evento is null) return NotFound(new { message = "Evento no encontrado." });

        // uploads/{tenantId}/{pacienteId}/{guid}{extension}
        // NOTA: esto guarda en el disco local. El día que se dockerice/despliegue
        // esto tiene que migrar a un storage externo (S3/Blob), porque el disco
        // de un contenedor no persiste entre despliegues.
        var carpeta = Path.Combine(_env.ContentRootPath, "uploads", evento.TenantId.ToString(), evento.PacienteId.ToString());
        Directory.CreateDirectory(carpeta);

        var nombreEnDisco = $"{Guid.NewGuid()}{extension}";
        var rutaCompleta = Path.Combine(carpeta, nombreEnDisco);

        await using (var stream = System.IO.File.Create(rutaCompleta))
        {
            await archivo.CopyToAsync(stream, ct);
        }

        var registro = new ArchivoOdontograma
        {
            TenantId = evento.TenantId,
            EventoOdontogramaId = evento.Id,
            NombreOriginal = archivo.FileName,
            RutaEnDisco = rutaCompleta,
            ContentType = contentTypeSeguro,
            TamanioBytes = archivo.Length
        };

        _db.ArchivosOdontograma.Add(registro);
        await _db.SaveChangesAsync(ct);

        return Ok(new ArchivoResponse(registro.Id, registro.NombreOriginal, registro.ContentType, registro.TamanioBytes, registro.FechaSubida));
    }

    /// <summary>Descarga un archivo adjunto. Requiere sesión (no son URLs públicas).</summary>
    [HttpGet("archivos/{archivoId}")]
    public async Task<IActionResult> DescargarArchivo(Guid archivoId, CancellationToken ct)
    {
        var archivo = await _db.ArchivosOdontograma.FirstOrDefaultAsync(a => a.Id == archivoId, ct);
        if (archivo is null) return NotFound();

        if (!System.IO.File.Exists(archivo.RutaEnDisco))
            return NotFound(new { message = "El archivo ya no está disponible en el servidor." });

        var stream = System.IO.File.OpenRead(archivo.RutaEnDisco);
        return File(stream, archivo.ContentType, archivo.NombreOriginal);
    }
}
