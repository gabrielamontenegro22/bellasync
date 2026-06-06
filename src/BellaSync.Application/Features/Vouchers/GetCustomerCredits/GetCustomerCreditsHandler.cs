using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Vouchers.GetCustomerCredits;

public sealed class GetCustomerCreditsHandler
    : IQueryHandler<GetCustomerCreditsQuery, IReadOnlyList<CustomerCreditResponse>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerCreditsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CustomerCreditResponse>>> HandleAsync(
        GetCustomerCreditsQuery query, CancellationToken ct)
    {
        // Filtro:
        //  - Voucher Validated (era anticipo en una cita confirmada)
        //  - RefundDecision = CreditPending (admin/recepción decidió "crédito")
        //  - RefundResolvedAt = null (no se cerró todavía)
        //  - La cita asociada pertenece al cliente que pedimos
        //  - NO es un voucher interno (un crédito interno no puede
        //    generar a su vez otro crédito; el dominio ya bloquea
        //    Refunded sobre internos, pero por defensa explícita
        //    también lo filtramos acá).
        //
        // No filtramos por "AmountApplied < ReportedAmount" en SQL porque
        // ReportedAmount es Money (value converter) y no se traduce en
        // expresiones. Pero RefundResolvedAt == null ya garantiza que hay
        // saldo (cuando se consume todo, ApplyCredit setea ResolvedAt).
        var rows = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Where(v => v.Status == PaymentVoucherStatus.Validated
                     && v.RefundDecision == DepositRefundDecision.CreditPending
                     && v.RefundResolvedAt == null
                     && v.IsInternalCredit == false
                     && v.Appointment!.CustomerId == query.CustomerId)
            .OrderBy(v => v.Appointment!.CancelledAt)
            .ToListAsync(ct);

        var responses = rows.Select(v => new CustomerCreditResponse
        {
            VoucherId = v.Id,
            AvailableAmount = v.AvailableCredit,
            OriginalAmount = v.ReportedAmount.Amount,
            OriginalServiceName = v.Appointment!.Service?.Name ?? string.Empty,
            OriginalAppointmentDate = v.Appointment.StartAt,
            GeneratedAt = v.Appointment.CancelledAt ?? v.ReceivedAt,
        }).ToList();

        return Result<IReadOnlyList<CustomerCreditResponse>>.Success(responses);
    }
}
