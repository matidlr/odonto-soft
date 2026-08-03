using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Sedes (lugares donde atiende) de un odontólogo. Cada odontólogo arranca
/// con una sede "Principal" automática (creada en la migración, o al crear
/// el odontólogo); acá se agregan sedes adicionales, con su propio horario
/// de atención. Los pacientes, tratamientos, presupuestos, etc. son los
/// mismos en todas las sedes del odontólogo — no cambian por sede.
/// </summary>
[ApiController]
[Route("api/sedes")]
[Authorize(Policy = "TenantActivo")]
public class SedesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SedesController(AppDbContext db)
    {
        _db = db;
    }

    public record SedeResponse(Guid Id, Guid OdontologoId, string Nombre, string? Direccion, bool EsPrincipal, bool Activa);

    private static SedeResponse AResponse(Sede s) => new(s.Id, s.OdontologoId, s.Nombre, s.Direccion, s.EsPrincipal, s.Activa);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? odontologoId, [FromQuery] bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Sedes.AsQueryable();
        if (odontologoId is Guid id) query = query.Where(s => s.OdontologoId == id);
        if (!incluirInactivas) query = query.Where(s => s.Activa);

        var sedes = await query.OrderByDescending(s => s.EsPrincipal).ThenBy(s => s.Nombre).ToListAsync(ct);
        return Ok(sedes.Select(AResponse));
    }

    public record CrearSedeRequest(Guid OdontologoId, string Nombre, string? Direccion);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearSedeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la sede es obligatorio." });

        var odontologo = await _db.Odontologos.FirstOrDefaultAsync(o => o.Id == request.OdontologoId, ct);
        if (odontologo is null) return BadRequest(new { message = "Odontólogo inválido." });

        var sede = new Sede
        {
            TenantId = odontologo.TenantId,
            OdontologoId = odontologo.Id,
            Nombre = request.Nombre,
            Direccion = request.Direccion,
            EsPrincipal = false,
            Activa = true
        };

        _db.Sedes.Add(sede);
        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(sede));
    }

    public record EditarSedeRequest(string Nombre, string? Direccion, bool Activa);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarSedeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la sede es obligatorio." });

        var sede = await _db.Sedes.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sede is null) return NotFound(new { message = "Sede no encontrada." });

        if (sede.EsPrincipal && !request.Activa)
            return BadRequest(new { message = "La sede principal no se puede desactivar." });

        sede.Nombre = request.Nombre;
        sede.Direccion = request.Direccion;
        sede.Activa = request.Activa;

        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(sede));
    }
}
