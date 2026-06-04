using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.CancelSubscription;
using BellaSync.Application.Features.Subscription.ChangePlan;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Application.Features.Subscription.ReportPayment;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints para que la admin del salón gestione su suscripción a
/// BellaSync. Solo SalonAdmin — la admin del salón es la que paga,
/// la recepción no toca plata de SaaS.
///
///   GET  /api/Subscription              → snapshot completo
///   POST /api/Subscription/change-plan  → cambia el plan (prorrateo en upgrade)
///   POST /api/Subscription/report-payment → reporta transferencia
///                                          (queda en validación)
///
/// El pago "real" lo aprueba el SuperAdmin de BellaSync desde
/// /api/SaasAdmin/subscriptions/* — anti-pasarela, hay humano en el medio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin")]
public class SubscriptionController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetSubscriptionQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPost("change-plan")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangePlanRequest request,
        [FromServices] ICommandHandler<ChangePlanCommand, SubscriptionResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangePlanCommand(request.PlanCode), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// La admin del salón reporta que hizo la transferencia. La factura
    /// queda en estado Reported. La suscripción NO se activa hasta que
    /// el SuperAdmin de BellaSync valide contra el extracto bancario.
    /// </summary>
    [HttpPost("report-payment")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportPayment(
        [FromBody] ReportPaymentRequest request,
        [FromServices] ICommandHandler<ReportPaymentCommand, SubscriptionResponse> handler,
        CancellationToken ct)
    {
        var cmd = new ReportPaymentCommand(request.PaymentMethod, request.Reference);
        var result = await handler.HandleAsync(cmd, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// La admin del salón cancela su suscripción a BellaSync. Bloqueado
    /// si hay un pago Reported esperando validación (esperar la decisión
    /// del SuperAdmin primero).
    /// </summary>
    [HttpPost("cancel")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        [FromBody] CancelSubscriptionRequest request,
        [FromServices] ICommandHandler<CancelSubscriptionCommand, SubscriptionResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new CancelSubscriptionCommand(request.Reason), ct);
        return result.ToActionResult();
    }
}

public sealed class ChangePlanRequest
{
    public string PlanCode { get; set; } = string.Empty;
}

public sealed class ReportPaymentRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

public sealed class CancelSubscriptionRequest
{
    /// <summary>Razón opcional de la cancelación — para feedback.</summary>
    public string? Reason { get; set; }
}
