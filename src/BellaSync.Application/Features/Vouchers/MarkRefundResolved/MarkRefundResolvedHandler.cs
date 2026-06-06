using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
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

            // Si la decisión es Refunded (devolución real al cliente),
            // crear automáticamente un Expense para que el cierre de caja
            // refleje la salida de plata. Sin esto, el total recaudado del
            // día queda inflado (el voucher cuenta como ingreso pero la
            // devolución no se ve en ningún lado).
            //
            // Para CreditPending NO creamos expense — la plata no salió,
            // solo se reservó como saldo para una cita futura.
            if (voucher.RefundDecision == DepositRefundDecision.Refunded)
            {
                var concept = $"Devolución anticipo: {voucher.Appointment?.Customer?.FullName ?? "cliente"}" +
                              (voucher.Appointment?.Service?.Name is { } svc ? $" — {svc}" : "");

                var refundExpense = Expense.Create(
                    tenantId: voucher.TenantId,
                    concept: concept,
                    amount: voucher.ReportedAmount,
                    method: PaymentMethod.Transfer,
                    provider: voucher.Bank ?? "Transferencia",
                    registeredByUserId: _currentUser.UserId,
                    utcNow: _clock.UtcNow);

                _db.Expenses.Add(refundExpense);
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Refund del voucher {VoucherId} marcado como resuelto por user {UserId}. Decision={Decision}, expense_auto={ExpenseAuto}",
                voucher.Id, _currentUser.UserId, voucher.RefundDecision,
                voucher.RefundDecision == DepositRefundDecision.Refunded);
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
