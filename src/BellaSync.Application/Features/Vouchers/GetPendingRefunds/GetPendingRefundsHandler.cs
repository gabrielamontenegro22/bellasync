using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Vouchers.GetPendingRefunds;

public sealed class GetPendingRefundsHandler
    : IQueryHandler<GetPendingRefundsQuery, IReadOnlyList<PendingRefundResponse>>
{
    private readonly IApplicationDbContext _db;

    public GetPendingRefundsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PendingRefundResponse>>> HandleAsync(
        GetPendingRefundsQuery query, CancellationToken ct)
    {
        // Filtros:
        //  - El voucher tiene una decisión registrada (RefundDecision != null)
        //  - Que NO sea Forfeited (esos se autoresuelven, no requieren acción)
        //  - Que todavía no se haya marcado como resuelto
        // El global filter ya restringe al tenant actual.
        var refunds = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .Where(v => v.RefundDecision != null
                     && v.RefundDecision != DepositRefundDecision.Forfeited
                     && v.RefundResolvedAt == null)
            // Más antiguas primero — son las que llevan más tiempo esperando
            // que la admin haga la transferencia.
            .OrderBy(v => v.Appointment!.CancelledAt)
            .Select(v => new PendingRefundResponse
            {
                VoucherId = v.Id,
                AppointmentId = v.AppointmentId,
                CustomerName = v.Appointment!.Customer!.FullName,
                CustomerPhone = v.Appointment.Customer.Phone,
                ServiceName = v.Appointment.Service!.Name,
                StylistName = v.Appointment.Stylist!.FullName,
                AppointmentStartAt = v.Appointment.StartAt,
                Amount = v.ReportedAmount.Amount,
                Bank = v.Bank,
                CancelledAt = v.Appointment.CancelledAt ?? DateTime.UtcNow,
                CancellationReason = v.Appointment.CancellationReason,
                Decision = v.RefundDecision!.Value.ToString(),
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<PendingRefundResponse>>.Success(refunds);
    }
}
