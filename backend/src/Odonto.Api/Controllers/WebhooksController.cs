using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odonto.Domain.Common;
using Odonto.Infrastructure.Payments;
using Odonto.Infrastructure.Persistence;

namespace Odonto.Api.Controllers;

/// <summary>
/// Recibe los avisos de Mercado Pago cuando cambia el estado de una
/// suscripción (autorizada, pausada, cancelada). No confiamos ciegamente
/// en el body del aviso: volvemos a consultar el estado real a la API de
/// Mercado Pago antes de tocar el tenant. Además valida la firma (HMAC)
/// para confirmar que el aviso viene realmente de Mercado Pago.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MercadoPagoService _mercadoPago;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        AppDbContext db,
        MercadoPagoService mercadoPago,
        IConfiguration configuration,
        ILogger<WebhooksController> logger)
    {
        _db = db;
        _mercadoPago = mercadoPago;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("mercadopago")]
    public async Task<IActionResult> MercadoPago(CancellationToken ct)
    {
        var type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
        var dataId = Request.Query["data.id"].FirstOrDefault() ?? Request.Query["id"].FirstOrDefault();
        var xSignature = Request.Headers["x-signature"].FirstOrDefault();
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault();

        _logger.LogInformation("Webhook Mercado Pago recibido: type={Type} dataId={DataId}", type, dataId);

        // No cortamos el flujo si la firma no valida (para no perder avisos
        // reales por algún detalle del formato), pero queda bien registrado
        // en el log para poder auditarlo.
        if (!ValidarFirma(xSignature, xRequestId, dataId, out var motivo))
        {
            _logger.LogWarning("Firma de webhook no verificada ({Motivo}) para preapproval {Id}. Se procesa igual.", motivo, dataId);
        }

        if (string.IsNullOrWhiteSpace(dataId) ||
            (type != "subscription_preapproval" && type != "preapproval"))
        {
            // Ignoramos otros tipos de aviso (pagos individuales, etc.) por ahora.
            return Ok();
        }

        string estadoMp;
        try
        {
            estadoMp = await _mercadoPago.ObtenerEstadoAsync(dataId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el preapproval {Id} en Mercado Pago.", dataId);
            return Ok(); // 200 igual, para que MP no reintente en loop por un error nuestro
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.MercadoPagoPreapprovalId == dataId, ct);
        if (tenant is null)
        {
            _logger.LogWarning("No se encontró tenant para el preapproval {Id}.", dataId);
            return Ok();
        }

        var estadoAnterior = tenant.Estado;
        tenant.Estado = estadoMp switch
        {
            "authorized" => TenantEstado.Activo,
            "paused" => TenantEstado.Suspendido,
            "cancelled" => TenantEstado.Suspendido,
            _ => tenant.Estado // "pending" u otro: todavía no cambiamos nada
        };

        if (tenant.Estado != estadoAnterior)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Tenant {TenantId} pasó de {Anterior} a {Nuevo} por webhook MP (preapproval {PreapprovalId}, estado_mp={EstadoMp}).",
                tenant.Id, estadoAnterior, tenant.Estado, dataId, estadoMp);
        }

        return Ok();
    }

    /// <summary>
    /// Valida el header x-signature (formato "ts=...,v1=...") armando el
    /// mismo manifest que usa Mercado Pago y comparando el HMAC-SHA256
    /// contra la clave secreta configurada.
    /// </summary>
    private bool ValidarFirma(string? xSignature, string? xRequestId, string? dataId, out string motivo)
    {
        var secret = _configuration["MercadoPago:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            motivo = "no hay WebhookSecret configurado";
            return false;
        }

        if (string.IsNullOrWhiteSpace(xSignature))
        {
            motivo = "falta el header x-signature";
            return false;
        }

        string? ts = null;
        string? v1 = null;
        foreach (var parte in xSignature.Split(','))
        {
            var kv = parte.Split('=', 2);
            if (kv.Length != 2) continue;

            var clave = kv[0].Trim();
            var valor = kv[1].Trim();
            if (clave == "ts") ts = valor;
            else if (clave == "v1") v1 = valor;
        }

        if (ts is null || v1 is null)
        {
            motivo = "x-signature no tiene el formato esperado (ts=...,v1=...)";
            return false;
        }

        var manifest = $"id:{dataId?.ToLowerInvariant()};request-id:{xRequestId};ts:{ts};";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest));
        var computado = Convert.ToHexString(hash).ToLowerInvariant();

        if (computado != v1)
        {
            motivo = "la firma calculada no coincide";
            return false;
        }

        motivo = "ok";
        return true;
    }
}
