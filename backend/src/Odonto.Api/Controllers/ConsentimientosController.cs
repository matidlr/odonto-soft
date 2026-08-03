using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Consentimientos informados de un paciente (general, cirugía, implante),
/// con firma manuscrita capturada digitalmente. Se puede crear un borrador
/// sin firmar y firmarlo después, o firmarlo en el momento.
/// </summary>
[ApiController]
[Authorize(Policy = "TenantActivo")]
public class ConsentimientosController : ControllerBase
{
    private const int TamanioMaximoFirmaBase64 = 700_000; // ~500 KB de imagen en base64

    private readonly AppDbContext _db;
    private readonly ILogger<ConsentimientosController> _logger;

    public ConsentimientosController(AppDbContext db, ILogger<ConsentimientosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public record ConsentimientoResponse(
        Guid Id,
        Guid PacienteId,
        Guid? OdontologoId,
        TipoConsentimiento Tipo,
        string Titulo,
        string Texto,
        string? FirmaBase64,
        string? FirmaNombreAclaratorio,
        DateTime? FechaFirma,
        bool Firmado,
        DateTime FechaCreacion);

    private static ConsentimientoResponse AResponse(Consentimiento c) => new(
        c.Id, c.PacienteId, c.OdontologoId, c.Tipo, c.Titulo, c.Texto,
        c.FirmaBase64, c.FirmaNombreAclaratorio, c.FechaFirma, c.FirmaBase64 != null, c.FechaCreacion);

    [HttpGet("api/pacientes/{pacienteId}/consentimientos")]
    public async Task<IActionResult> GetPorPaciente(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var consentimientos = await _db.Consentimientos
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync(ct);

        return Ok(consentimientos.Select(AResponse));
    }

    [HttpGet("api/consentimientos/{id}")]
    public async Task<IActionResult> GetPorId(Guid id, CancellationToken ct)
    {
        var consentimiento = await _db.Consentimientos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (consentimiento is null) return NotFound();

        return Ok(AResponse(consentimiento));
    }

    public record CrearConsentimientoRequest(
        TipoConsentimiento Tipo,
        string Titulo,
        string Texto,
        Guid? OdontologoId,
        // Se puede firmar en el mismo paso (el paciente firma ahí en pantalla) o dejarlo sin firmar.
        string? FirmaBase64,
        string? FirmaNombreAclaratorio);

    [HttpPost("api/pacientes/{pacienteId}/consentimientos")]
    public async Task<IActionResult> Crear(Guid pacienteId, CrearConsentimientoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) || request.Titulo.Length > 200)
            return BadRequest(new { message = "El título es obligatorio y no puede superar los 200 caracteres." });
        if (string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest(new { message = "El texto del consentimiento es obligatorio." });
        if (!Validaciones.EsEnumValido(request.Tipo))
            return BadRequest(new { message = "Tipo de consentimiento inválido." });
        if (request.FirmaBase64 is not null && request.FirmaBase64.Length > TamanioMaximoFirmaBase64)
            return BadRequest(new { message = "La firma es demasiado grande." });

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

        var tieneFirma = !string.IsNullOrWhiteSpace(request.FirmaBase64);

        var consentimiento = new Consentimiento
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            OdontologoId = request.OdontologoId,
            Tipo = request.Tipo,
            Titulo = request.Titulo,
            Texto = request.Texto,
            FirmaBase64 = tieneFirma ? request.FirmaBase64 : null,
            FirmaNombreAclaratorio = tieneFirma ? request.FirmaNombreAclaratorio : null,
            FechaFirma = tieneFirma ? DateTime.UtcNow : null
        };

        _db.Consentimientos.Add(consentimiento);
        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(consentimiento));
    }

    public record FirmarConsentimientoRequest(string FirmaBase64, string? FirmaNombreAclaratorio);

    [HttpPost("api/consentimientos/{id}/firmar")]
    public async Task<IActionResult> Firmar(Guid id, FirmarConsentimientoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirmaBase64))
            return BadRequest(new { message = "Falta la firma." });
        if (request.FirmaBase64.Length > TamanioMaximoFirmaBase64)
            return BadRequest(new { message = "La firma es demasiado grande." });

        var consentimiento = await _db.Consentimientos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (consentimiento is null) return NotFound();

        if (consentimiento.FirmaBase64 is not null)
            return BadRequest(new { message = "Este consentimiento ya está firmado." });

        consentimiento.FirmaBase64 = request.FirmaBase64;
        consentimiento.FirmaNombreAclaratorio = request.FirmaNombreAclaratorio;
        consentimiento.FechaFirma = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(consentimiento));
    }

    [HttpDelete("api/consentimientos/{id}")]
    public async Task<IActionResult> Borrar(Guid id, CancellationToken ct)
    {
        var consentimiento = await _db.Consentimientos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (consentimiento is null) return NotFound();

        if (consentimiento.FirmaBase64 is not null)
            return BadRequest(new { message = "No se puede borrar un consentimiento ya firmado." });

        consentimiento.IsDeleted = true;
        consentimiento.DeletedAt = DateTime.UtcNow;
        consentimiento.DeletedBy = UsuarioIdActual();
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Consentimiento {ConsentimientoId} eliminado (baja lógica) por usuario {UsuarioId}",
            consentimiento.Id, UsuarioIdActual());

        return Ok(new { message = "Consentimiento eliminado." });
    }
}
