using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Infrastructure.Payments;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Lo usa el odontólogo/owner para activar su cuenta pagando la suscripción.
/// No lleva la policy TenantActivo (obviamente: es justo lo que falta).
/// </summary>
[ApiController]
[Route("api/suscripcion")]
[Authorize]
public class SuscripcionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MercadoPagoService _mercadoPago;

    public SuscripcionController(AppDbContext db, MercadoPagoService mercadoPago)
    {
        _db = db;
        _mercadoPago = mercadoPago;
    }

    // payerEmailPrueba: SOLO para probar en sandbox con una cuenta de prueba
    // "Comprador" (Mercado Pago exige que el payer_email exista de verdad
    // en modo test). En producción no se manda y se usa el email del login.
    [HttpPost("iniciar-pago")]
    public async Task<IActionResult> IniciarPago([FromQuery] string? payerEmailPrueba, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario (¿sos SuperAdmin?)." });

        var email = string.IsNullOrWhiteSpace(payerEmailPrueba)
            ? User.FindFirst("email")?.Value
            : payerEmailPrueba;

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "No se pudo determinar el email del usuario." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        var (preapprovalId, initPoint) = await _mercadoPago.CrearSuscripcionAsync(
            tenant.Id, email, $"Suscripcion Odonto SaaS - {tenant.Nombre}", ct);

        tenant.MercadoPagoPreapprovalId = preapprovalId;
        await _db.SaveChangesAsync(ct);

        return Ok(new { initPoint, preapprovalId });
    }
}
