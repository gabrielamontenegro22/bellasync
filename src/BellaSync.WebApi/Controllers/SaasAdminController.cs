using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.Dtos;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.ListPendingValidations;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.RejectPayment;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.ValidatePayment;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Panel del SuperAdmin (dueño de BellaSync). Cross-tenant: ve y
/// gestiona pagos de suscripción de TODOS los salones-clientes.
///
///   GET  /api/SaasAdmin/subscriptions/pending-validations
///   POST /api/SaasAdmin/subscriptions/invoices/{id}/validate
///   POST /api/SaasAdmin/subscriptions/invoices/{id}/reject
///
/// El SuperAdmin loguea igual que cualquier user (email + password)
/// pero su User.Role = SuperAdmin y User.TenantId = Guid.Empty, lo
/// que le permite escapar el filtro multi-tenant en estos endpoints.
/// </summary>
[ApiController]
[Route("api/SaasAdmin/subscriptions")]
[Authorize(Roles = "SuperAdmin")]
public class SaasAdminController : ControllerBase
{
    [HttpGet("pending-validations")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingValidationRow>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(
        [FromServices] IQueryHandler<ListPendingValidationsQuery, IReadOnlyList<PendingValidationRow>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListPendingValidationsQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPost("invoices/{id:guid}/validate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Validate(
        Guid id,
        [FromServices] ICommandHandler<ValidatePaymentCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ValidatePaymentCommand(id), ct);
        return result.ToActionResult();
    }

    [HttpPost("invoices/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectPaymentRequest request,
        [FromServices] ICommandHandler<RejectPaymentCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new RejectPaymentCommand(id, request.Reason), ct);
        return result.ToActionResult();
    }
}

public sealed class RejectPaymentRequest
{
    public string Reason { get; set; } = string.Empty;
}
