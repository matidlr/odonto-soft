using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Odontólogos del tenant actual. El primero se crea junto con el Owner al
/// registrar la clínica (con login propio); los siguientes se agregan acá
/// como perfiles de datos nada más — la clínica tiene un solo login
/// compartido, y el selector de "qué odontólogo estoy usando" en el
/// frontend elige entre estos perfiles.
/// </summary>
[ApiController]
[Route("api/odontologos")]
[Authorize(Policy = "TenantActivo")]
public class OdontologosController : ControllerBase
{
    private readonly AppDbContext _db;

    public OdontologosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var odontologos = await _db.Odontologos
            .OrderBy(o => o.Nombre)
            .Select(o => new
            {
                o.Id,
                o.Nombre,
                o.Matricula,
                o.Especialidad,
                o.ColorAgenda
            })
            .ToListAsync(ct);

        return Ok(odontologos);
    }

    public record CrearOdontologoRequest(string Nombre, string Matricula, string? Especialidad, string? ColorAgenda);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearOdontologoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.Matricula))
            return BadRequest(new { message = "La matrícula es obligatoria." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var tenant = await _db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return BadRequest(new { message = "No se encontró la clínica del usuario." });

        // Sin plan asignado (no debería pasar, pero por las dudas) se trata
        // como el límite más restrictivo: 1 odontólogo.
        var maxOdontologos = tenant.Plan?.MaxOdontologos ?? 1;
        var cantidadActual = await _db.Odontologos.CountAsync(o => o.TenantId == tenantId, ct);

        if (cantidadActual >= maxOdontologos)
        {
            return BadRequest(new
            {
                message = $"Tu plan ({tenant.Plan?.Nombre ?? "sin definir"}) permite hasta {maxOdontologos} odontólogo(s). Para agregar más, pedile al SuperAdmin que suba tu plan."
            });
        }

        var odontologo = new Odontologo
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            Matricula = request.Matricula,
            Especialidad = request.Especialidad,
            ColorAgenda = string.IsNullOrWhiteSpace(request.ColorAgenda) ? "#2563eb" : request.ColorAgenda
        };

        _db.Odontologos.Add(odontologo);
        await _db.SaveChangesAsync(ct);

        return Ok(new { odontologo.Id });
    }
}
