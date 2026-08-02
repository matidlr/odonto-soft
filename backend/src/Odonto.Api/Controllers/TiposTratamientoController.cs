using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

[ApiController]
[Route("api/tipos-tratamiento")]
[Authorize(Policy = "TenantActivo")]
public class TiposTratamientoController : ControllerBase
{
    private readonly AppDbContext _db;

    public TiposTratamientoController(AppDbContext db)
    {
        _db = db;
    }

    public record CrearTipoTratamientoRequest(string Nombre, int DuracionMinutos, decimal PrecioBase, string? Observaciones);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearTipoTratamientoRequest request, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tipo = new TipoTratamiento
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            DuracionMinutos = request.DuracionMinutos,
            PrecioBase = request.PrecioBase,
            Observaciones = request.Observaciones
        };

        _db.TiposTratamiento.Add(tipo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { tipo.Id });
    }

    public record EditarTipoTratamientoRequest(string Nombre, int DuracionMinutos, decimal PrecioBase, string? Observaciones);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarTipoTratamientoRequest request, CancellationToken ct)
    {
        var tipo = await _db.TiposTratamiento.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tipo is null) return NotFound(new { message = "Tipo de tratamiento no encontrado." });

        tipo.Nombre = request.Nombre;
        tipo.DuracionMinutos = request.DuracionMinutos;
        tipo.PrecioBase = request.PrecioBase;
        tipo.Observaciones = request.Observaciones;

        await _db.SaveChangesAsync(ct);

        return Ok(new { tipo.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tipos = await _db.TiposTratamiento
            .Select(t => new { t.Id, t.Nombre, t.DuracionMinutos, t.PrecioBase, t.Observaciones })
            .ToListAsync(ct);

        return Ok(tipos);
    }
}
