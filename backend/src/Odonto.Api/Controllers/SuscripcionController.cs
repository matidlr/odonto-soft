using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Api.Payments;
using Odonto.Infrastructure.Payments;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Lo usa el odontólogo/owner para activar su cuenta pagando la suscripción.
/// No lleva la policy TenantActivo (obviamente: es justo lo que falta).
/// </summary>
[ApiController]
[Route("api/v1/suscripcion")]
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

    public record IniciarPagoRequest(Guid PlanId, string? PayerEmailPrueba);

    // PayerEmailPrueba: SOLO para probar en sandbox con una cuenta de prueba
    // "Comprador" (Mercado Pago exige que el payer_email exista de verdad
    // en modo test). En producción no se manda y se usa el email del login.
    [HttpPost("iniciar-pago")]
    public async Task<IActionResult> IniciarPago(IniciarPagoRequest request, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario (¿sos SuperAdmin?)." });

        // El JWT ya no lleva el email (solo UserId, ClinicaId y Rol); si no
        // vino uno de prueba, lo buscamos en la base a partir del UsuarioId.
        var email = request.PayerEmailPrueba;
        if (string.IsNullOrWhiteSpace(email))
        {
            var usuarioIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(usuarioIdClaim, out var usuarioId))
            {
                email = await _db.Usuarios.IgnoreQueryFilters()
                    .Where(u => u.Id == usuarioId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
            }
        }

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "No se pudo determinar el email del usuario." });

        var plan = await _db.Planes.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.Activo, ct);
        if (plan is null) return BadRequest(new { message = "Plan inválido." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        var cantidadActual = await _db.Odontologos.CountAsync(o => o.TenantId == tenantId, ct);
        if (cantidadActual > plan.MaxOdontologos)
        {
            return BadRequest(new
            {
                message = $"Tenés {cantidadActual} odontólogo(s) cargados y el plan {plan.Nombre} permite hasta {plan.MaxOdontologos}. Elegí un plan más grande."
            });
        }

        var (preapprovalId, initPoint) = await _mercadoPago.CrearSuscripcionAsync(
            tenant.Id, email, $"Suscripcion Odonto SaaS - {tenant.Nombre} - Plan {plan.Nombre}", plan.PrecioMensual, ct);

        tenant.MercadoPagoPreapprovalId = preapprovalId;
        tenant.PlanId = plan.Id;
        await _db.SaveChangesAsync(ct);

        return Ok(new { initPoint, preapprovalId });
    }

    /// <summary>
    /// Le pregunta directo a Mercado Pago el estado actual de la suscripción
    /// y actualiza el tenant, sin depender de que el webhook haya llegado.
    /// Pensado para: (a) desarrollo local, donde MP no puede pegarle al
    /// webhook porque no hay URL pública, y (b) como red de seguridad en
    /// producción si el webhook se pierde o se demora — el usuario puede
    /// volver a esta pantalla después de pagar y forzar la actualización.
    /// No lleva TenantActivo (es lo que justamente puede estar arreglando).
    /// </summary>
    [HttpPost("sincronizar-estado")]
    public async Task<IActionResult> SincronizarEstado(CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            return BadRequest(new { message = "No se pudo determinar el tenant del usuario (¿sos SuperAdmin?)." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.MercadoPagoPreapprovalId))
            return BadRequest(new { message = "Todavía no iniciaste ningún pago para esta clínica." });

        var estadoMp = await _mercadoPago.ObtenerEstadoAsync(tenant.MercadoPagoPreapprovalId, ct);
        var cambio = EstadoMercadoPagoMapper.Aplicar(tenant, estadoMp);

        if (cambio)
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { estado = tenant.Estado.ToString(), tienePagoActivo = tenant.TienePagoActivo, estadoMercadoPago = estadoMp });
    }
}
