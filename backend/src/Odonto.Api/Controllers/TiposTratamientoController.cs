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

    public record CrearTipoTratamientoRequest(string Nombre, int DuracionMinutos, decimal PrecioBase);

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
            PrecioBase = request.PrecioBase
        };

        _db.TiposTratamiento.Add(tipo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { tipo.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tipos = await _db.TiposTratamiento
            .Select(t => new { t.Id, t.Nombre, t.DuracionMinutos, t.PrecioBase })
            .ToListAsync(ct);

        return Ok(tipos);
    }
}
