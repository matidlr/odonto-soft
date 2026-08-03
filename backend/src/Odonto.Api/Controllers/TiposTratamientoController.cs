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

    /// <summary>Reglas comunes a Crear/Editar: nunca confiar en que Angular ya validó esto.</summary>
    private static string? ValidarTipoTratamiento(string nombre, int duracionMinutos, decimal precioBase)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 150)
            return "El nombre es obligatorio y no puede superar los 150 caracteres.";
        if (duracionMinutos <= 0 || duracionMinutos > 600)
            return "La duración tiene que ser mayor a 0 y no puede superar los 600 minutos.";
        if (precioBase < 0)
            return "El precio no puede ser negativo.";
        return null;
    }

    public record CrearTipoTratamientoRequest(string Nombre, int DuracionMinutos, decimal PrecioBase, string? Observaciones);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearTipoTratamientoRequest request, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var error = ValidarTipoTratamiento(request.Nombre, request.DuracionMinutos, request.PrecioBase);
        if (error is not null) return BadRequest(new { message = error });

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

        var error = ValidarTipoTratamiento(request.Nombre, request.DuracionMinutos, request.PrecioBase);
        if (error is not null) return BadRequest(new { message = error });

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
