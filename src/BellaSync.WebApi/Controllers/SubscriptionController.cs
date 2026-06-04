using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.ChangePlan;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Application.Features.Subscription.MarkInvoicePaid;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints para que la admin del salón gestione su suscripción a
/// BellaSync (el SaaS). Solo SalonAdmin — receptionists no tocan plata
/// de suscripción.
///
///   GET    /api/Subscription                    → snapshot completo
///   POST   /api/Subscription/change-plan        → cambia el plan
///   POST   /api/Subscription/invoices/{id}/pay  → marca factura paga
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

    [HttpPost("invoices/{id:guid}/pay")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PayInvoice(
        Guid id,
        [FromBody] PayInvoiceRequest request,
        [FromServices] ICommandHandler<MarkInvoicePaidCommand, SubscriptionResponse> handler,
        CancellationToken ct)
    {
        var cmd = new MarkInvoicePaidCommand(id, request.PaymentMethod, request.Reference);
        var result = await handler.HandleAsync(cmd, ct);
        return result.ToActionResult();
    }
}

public sealed class ChangePlanRequest
{
    public string PlanCode { get; set; } = string.Empty;
}

public sealed class PayInvoiceRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Reference { get; set; }
}
