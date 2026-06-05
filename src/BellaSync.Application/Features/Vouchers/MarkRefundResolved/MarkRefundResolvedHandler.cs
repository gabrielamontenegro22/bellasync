using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Vouchers.MarkRefundResolved;

public sealed class MarkRefundResolvedHandler
    : ICommandHandler<MarkRefundResolvedCommand, PendingRefundResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MarkRefundResolvedHandler> _logger;

    public MarkRefundResolvedHandler(
        IApplicationDbContext db,
        IClock clock,
        ICurrentUserService currentUser,
        ILogger<MarkRefundResolvedHandler> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PendingRefundResponse>> HandleAsync(
        MarkRefundResolvedCommand command, CancellationToken ct)
    {
        var voucher = await _db.PaymentVouchers
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .FirstOrDefaultAsync(v => v.Id == command.VoucherId, ct);

        if (voucher is null)
            return ApplicationError.NotFound("voucher.not_found",
                "No existe el voucher.");

        if (voucher.RefundDecision is null)
            return ApplicationError.Validation("voucher.no_refund_pending",
                "Este voucher no tiene una devolución pendiente.");

        if (voucher.RefundDecision == DepositRefundDecision.Forfeited)
            return ApplicationError.Validation("voucher.forfeited",
                "Este anticipo se marcó como perdido — no requiere acción.");

        // Idempotente: si ya estaba resuelto, no reescribimos la fecha
        // (preservar la real). Solo mapeamos y devolvemos.
        if (voucher.RefundResolvedAt is null)
        {
            try
            {
                voucher.MarkRefundResolved(_clock.UtcNow, _currentUser.UserId ?? Guid.Empty);
            }
            catch (DomainException ex)
            {
                return ApplicationError.Validation("voucher.invalid_state", ex.Message);
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Refund del voucher {VoucherId} marcado como resuelto por user {UserId}",
                voucher.Id, _currentUser.UserId);
        }

        return Result<PendingRefundResponse>.Success(new PendingRefundResponse
        {
            VoucherId = voucher.Id,
            AppointmentId = voucher.AppointmentId,
            CustomerName = voucher.Appointment!.Customer!.FullName,
            CustomerPhone = voucher.Appointment.Customer.Phone,
            ServiceName = voucher.Appointment.Service!.Name,
            StylistName = voucher.Appointment.Stylist!.FullName,
            AppointmentStartAt = voucher.Appointment.StartAt,
            Amount = voucher.ReportedAmount.Amount,
            Bank = voucher.Bank,
            CancelledAt = voucher.Appointment.CancelledAt ?? DateTime.UtcNow,
            CancellationReason = voucher.Appointment.CancellationReason,
            Decision = voucher.RefundDecision!.Value.ToString(),
        });
    }
}
