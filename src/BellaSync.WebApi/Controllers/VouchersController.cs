using System.Security.Claims;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.CreateVoucher;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Application.Features.Vouchers.ListPendingVouchers;
using BellaSync.Application.Features.Vouchers.ValidateVoucher;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Cola de validación de pagos. SalonAdmin + Receptionist pueden listar
/// vouchers pendientes y decidir. La creación interna (POST sin auth)
/// queda como stub para el webhook de WhatsApp; ahora se usa para tests
/// y como endpoint manual (recepción registra comprobante de email/audio).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class VouchersController : ControllerBase
{
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<VoucherResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(
        [FromServices] IQueryHandler<ListPendingVouchersQuery, IReadOnlyList<VoucherResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListPendingVouchersQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(VoucherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVoucherCommand command,
        [FromServices] ICommandHandler<CreateVoucherCommand, VoucherResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(VoucherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validate(
        Guid id,
        [FromBody] ValidateVoucherRequest request,
        [FromServices] ICommandHandler<ValidateVoucherCommand, VoucherResponse> handler,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? Guid.Empty.ToString();

        var command = new ValidateVoucherCommand(
            VoucherId: id,
            Decision: request.Decision,
            DecidedByUserId: Guid.TryParse(userId, out var g) ? g : Guid.Empty,
            Notes: request.Notes);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    public sealed record ValidateVoucherRequest(VoucherDecision Decision, string? Notes);
}
