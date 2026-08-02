using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Domain.Entities;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Inventario de insumos de la clínica (anestesia, guantes, resinas,
/// implantes, materiales). El stock actual solo cambia a través de
/// movimientos (entradas/salidas), nunca se edita directo — así queda
/// historial de cada carga o descuento.
/// </summary>
[ApiController]
[Route("api/insumos")]
[Authorize(Policy = "TenantActivo")]
public class InsumosController : ControllerBase
{
    private readonly AppDbContext _db;

    public InsumosController(AppDbContext db)
    {
        _db = db;
    }

    public record InsumoResponse(
        Guid Id,
        string Nombre,
        CategoriaInsumo Categoria,
        string Unidad,
        decimal StockActual,
        decimal StockMinimo,
        bool StockBajo,
        bool Activo);

    private static InsumoResponse AResponse(Insumo i) => new(
        i.Id, i.Nombre, i.Categoria, i.Unidad, i.StockActual, i.StockMinimo, i.StockActual < i.StockMinimo, i.Activo);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.Insumos.AsQueryable();
        if (!incluirInactivos) query = query.Where(i => i.Activo);

        var insumos = await query.OrderBy(i => i.Nombre).ToListAsync(ct);
        return Ok(insumos.Select(AResponse));
    }

    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas(CancellationToken ct)
    {
        var insumos = await _db.Insumos.Where(i => i.Activo).ToListAsync(ct);
        var conStockBajo = insumos.Where(i => i.StockActual < i.StockMinimo).Select(AResponse);
        return Ok(conStockBajo);
    }

    public record CrearInsumoRequest(string Nombre, CategoriaInsumo Categoria, string Unidad, decimal StockMinimo, decimal StockInicial);

    [HttpPost]
    public async Task<IActionResult> Crear(CrearInsumoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });
        if (request.StockMinimo < 0 || request.StockInicial < 0)
            return BadRequest(new { message = "El stock no puede ser negativo." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        var insumo = new Insumo
        {
            TenantId = tenantId,
            Nombre = request.Nombre,
            Categoria = request.Categoria,
            Unidad = string.IsNullOrWhiteSpace(request.Unidad) ? "unidades" : request.Unidad,
            StockMinimo = request.StockMinimo,
            StockActual = request.StockInicial
        };

        _db.Insumos.Add(insumo);

        if (request.StockInicial > 0)
        {
            _db.MovimientosStock.Add(new MovimientoStock
            {
                TenantId = tenantId,
                InsumoId = insumo.Id,
                Cantidad = request.StockInicial,
                Motivo = "Stock inicial"
            });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(insumo));
    }

    public record EditarInsumoRequest(string Nombre, CategoriaInsumo Categoria, string Unidad, decimal StockMinimo, bool Activo);

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(Guid id, EditarInsumoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });
        if (request.StockMinimo < 0)
            return BadRequest(new { message = "El stock mínimo no puede ser negativo." });

        var insumo = await _db.Insumos.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (insumo is null) return NotFound(new { message = "Insumo no encontrado." });

        insumo.Nombre = request.Nombre;
        insumo.Categoria = request.Categoria;
        insumo.Unidad = string.IsNullOrWhiteSpace(request.Unidad) ? "unidades" : request.Unidad;
        insumo.StockMinimo = request.StockMinimo;
        insumo.Activo = request.Activo;

        await _db.SaveChangesAsync(ct);

        return Ok(AResponse(insumo));
    }

    public record MovimientoResponse(Guid Id, decimal Cantidad, string? Motivo, DateTime Fecha);

    [HttpGet("{id}/movimientos")]
    public async Task<IActionResult> GetMovimientos(Guid id, CancellationToken ct)
    {
        var existeInsumo = await _db.Insumos.AnyAsync(i => i.Id == id, ct);
        if (!existeInsumo) return NotFound(new { message = "Insumo no encontrado." });

        var movimientos = await _db.MovimientosStock
            .Where(m => m.InsumoId == id)
            .OrderByDescending(m => m.Fecha)
            .Select(m => new MovimientoResponse(m.Id, m.Cantidad, m.Motivo, m.Fecha))
            .ToListAsync(ct);

        return Ok(movimientos);
    }

    public record CrearMovimientoRequest(decimal Cantidad, string? Motivo);

    /// <summary>Cantidad positiva = entrada (compra/reposición), negativa = salida (uso/merma).</summary>
    [HttpPost("{id}/movimientos")]
    public async Task<IActionResult> CrearMovimiento(Guid id, CrearMovimientoRequest request, CancellationToken ct)
    {
        if (request.Cantidad == 0)
            return BadRequest(new { message = "La cantidad no puede ser 0." });

        var insumo = await _db.Insumos.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (insumo is null) return NotFound(new { message = "Insumo no encontrado." });

        var nuevoStock = insumo.StockActual + request.Cantidad;
        if (nuevoStock < 0)
            return BadRequest(new { message = $"No hay stock suficiente. Stock actual: {insumo.StockActual} {insumo.Unidad}." });

        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario." });

        insumo.StockActual = nuevoStock;

        var movimiento = new MovimientoStock
        {
            TenantId = tenantId,
            InsumoId = id,
            Cantidad = request.Cantidad,
            Motivo = request.Motivo
        };
        _db.MovimientosStock.Add(movimiento);

        await _db.SaveChangesAsync(ct);

        return Ok(new { Insumo = AResponse(insumo), Movimiento = new MovimientoResponse(movimiento.Id, movimiento.Cantidad, movimiento.Motivo, movimiento.Fecha) });
    }
}
