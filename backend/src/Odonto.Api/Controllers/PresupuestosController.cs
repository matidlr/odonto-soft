using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Validacion;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Presupuestos de tratamiento por paciente: se cargan con ítems (cada uno
/// con precio, y opcionalmente un diente puntual), el paciente los aprueba o
/// rechaza, y uno ya Aprobado se puede "convertir" una sola vez — los ítems
/// con diente asignado pasan a ser eventos Planificados en el odontograma.
/// </summary>
[ApiController]
[Authorize(Policy = "TenantActivo")]
public class PresupuestosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<PresupuestosController> _logger;

    public PresupuestosController(AppDbContext db, ILogger<PresupuestosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private Guid? UsuarioIdActual()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public record ItemResponse(
        Guid Id,
        Guid? TipoTratamientoId,
        string Descripcion,
        int? NumeroFdi,
        EstadoDiente? EstadoDienteResultante,
        int Cantidad,
        decimal PrecioUnitario,
        decimal Subtotal);

    public record PresupuestoResponse(
        Guid Id,
        Guid PacienteId,
        Guid? OdontologoId,
        EstadoPresupuesto Estado,
        string? Observaciones,
        bool Convertido,
        DateTime? FechaConversion,
        DateTime FechaCreacion,
        DateTime? FechaRespuesta,
        decimal MontoTotal,
        List<ItemResponse> Items);

    private static PresupuestoResponse AResponse(Presupuesto p) => new(
        p.Id,
        p.PacienteId,
        p.OdontologoId,
        p.Estado,
        p.Observaciones,
        p.Convertido,
        p.FechaConversion,
        p.FechaCreacion,
        p.FechaRespuesta,
        p.Items.Sum(i => i.Cantidad * i.PrecioUnitario),
        p.Items.Select(i => new ItemResponse(
            i.Id, i.TipoTratamientoId, i.Descripcion, i.NumeroFdi, i.EstadoDienteResultante,
            i.Cantidad, i.PrecioUnitario, i.Cantidad * i.PrecioUnitario)).ToList());

    [HttpGet("api/pacientes/{pacienteId}/presupuestos")]
    public async Task<IActionResult> GetPorPaciente(Guid pacienteId, CancellationToken ct)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null) return NotFound(new { message = "Paciente no encontrado." });

        var presupuestos = await _db.Presupuestos
            .Include(p => p.Items)
            .Where(p => p.PacienteId == pacienteId)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync(ct);

        return Ok(presupuestos.Select(AResponse));
    }

    [HttpGet("api/presupuestos/{id}")]
    public async Task<IActionResult> GetPorId(Guid id, CancellationToken ct)
    {
        var presupuesto = await _db.Presupuestos.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (presupuesto is null) return NotFound();

        return Ok(AResponse(presupuesto));
    }

    public record ItemRequest(
        Guid? TipoTratamientoId,
        string Descripcion,
        int? NumeroFdi,
        EstadoDiente? EstadoDienteResultante,
        int Cantidad,
        decimal PrecioUnitario);

    public record CrearPresupuestoRequest(
        Guid? OdontologoId,
        string? Observaciones,
        List<ItemRequest> Items);

    [HttpPost("api/pacientes/{pacienteId}/presupuestos")]
    public async Task<IActionResult> Crear(Guid pacienteId, CrearPresupuestoRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "El presupuesto necesita al menos un ítem." });

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Descripcion) || item.Descripcion.Length > 300)
                return BadRequest(new { message = "Cada ítem necesita una descripción de hasta 300 caracteres." });
            if (item.Cantidad <= 0)
                return BadRequest(new { message = "La cantidad de cada ítem debe ser mayor a 0." });
            if (item.PrecioUnitario < 0)
                return BadRequest(new { message = "El precio de cada ítem no puede ser negativo." });
            if (item.NumeroFdi is null && item.EstadoDienteResultante is not null)
                return BadRequest(new { message = "Si indicás el estado resultante, también tenés que indicar el diente." });
            if (!Validaciones.EsNumeroFdiValido(item.NumeroFdi))
                return BadRequest(new { message = "Número de pieza dental inválido." });
            if (!Validaciones.EsEnumValido(item.EstadoDienteResultante))
                return BadRequest(new { message = "Estado de diente resultante inválido." });
        }

        if (request.Observaciones?.Length > 1000)
            return BadRequest(new { message = "Las observaciones no pueden superar los 1000 caracteres." });

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

        var presupuesto = new Presupuesto
        {
            TenantId = tenantId,
            PacienteId = pacienteId,
            OdontologoId = request.OdontologoId,
            Observaciones = request.Observaciones,
            Items = request.Items.Select(i => new ItemPresupuesto
            {
                TipoTratamientoId = i.TipoTratamientoId,
                Descripcion = i.Descripcion,
                NumeroFdi = i.NumeroFdi,
                EstadoDienteResultante = i.EstadoDienteResultante,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario
            }).ToList()
        };

        _db.Presupuestos.Add(presupuesto);
        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(presupuesto));
    }

    public record CambiarEstadoRequest(EstadoPresupuesto Estado);

    [HttpPut("api/presupuestos/{id}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, CambiarEstadoRequest request, CancellationToken ct)
    {
        if (!Validaciones.EsEnumValido(request.Estado))
            return BadRequest(new { message = "Estado inválido." });

        if (request.Estado == EstadoPresupuesto.Pendiente)
            return BadRequest(new { message = "No se puede volver un presupuesto a Pendiente." });

        var presupuesto = await _db.Presupuestos.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (presupuesto is null) return NotFound();

        if (presupuesto.Convertido)
            return BadRequest(new { message = "Este presupuesto ya fue convertido en tratamiento, no se puede cambiar." });

        presupuesto.Estado = request.Estado;
        presupuesto.FechaRespuesta = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(presupuesto));
    }

    /// <summary>
    /// Convierte un presupuesto Aprobado en tratamiento: cada ítem con
    /// diente y estado resultante asignados genera un EventoOdontograma
    /// Planificado. Solo se puede hacer una vez.
    /// </summary>
    [HttpPost("api/presupuestos/{id}/convertir")]
    public async Task<IActionResult> Convertir(Guid id, CancellationToken ct)
    {
        var presupuesto = await _db.Presupuestos.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (presupuesto is null) return NotFound();

        if (presupuesto.Estado != EstadoPresupuesto.Aprobado)
            return BadRequest(new { message = "Solo se puede convertir un presupuesto Aprobado." });

        if (presupuesto.Convertido)
            return BadRequest(new { message = "Este presupuesto ya fue convertido." });

        var ahora = DateTime.UtcNow;
        var eventosCreados = 0;

        foreach (var item in presupuesto.Items)
        {
            if (item.NumeroFdi is int numeroFdi && item.EstadoDienteResultante is EstadoDiente estado)
            {
                _db.EventosOdontograma.Add(new EventoOdontograma
                {
                    TenantId = presupuesto.TenantId,
                    PacienteId = presupuesto.PacienteId,
                    NumeroFdi = numeroFdi,
                    Estado = estado,
                    EstadoTratamiento = EstadoTratamiento.Planificado,
                    Tratamiento = item.Descripcion,
                    Nota = "Generado desde presupuesto aprobado.",
                    OdontologoId = presupuesto.OdontologoId,
                    Fecha = ahora
                });
                eventosCreados++;
            }
        }

        presupuesto.Convertido = true;
        presupuesto.FechaConversion = ahora;
        await _db.SaveChangesAsync(ct);

        return Ok(new { presupuesto.Id, EventosCreados = eventosCreados });
    }

    [HttpDelete("api/presupuestos/{id}")]
    public async Task<IActionResult> Borrar(Guid id, CancellationToken ct)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (presupuesto is null) return NotFound();

        if (presupuesto.Estado != EstadoPresupuesto.Pendiente)
            return BadRequest(new { message = "Solo se pueden borrar presupuestos Pendientes." });

        presupuesto.IsDeleted = true;
        presupuesto.DeletedAt = DateTime.UtcNow;
        presupuesto.DeletedBy = UsuarioIdActual();
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Presupuesto {PresupuestoId} eliminado (baja lógica) por usuario {UsuarioId}",
            presupuesto.Id, UsuarioIdActual());

        return Ok(new { message = "Presupuesto eliminado." });
    }
}
