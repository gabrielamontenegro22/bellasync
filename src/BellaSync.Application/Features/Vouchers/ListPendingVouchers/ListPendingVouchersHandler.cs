using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Application.Features.Vouchers.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Vouchers.ListPendingVouchers;

public sealed class ListPendingVouchersHandler
    : IQueryHandler<ListPendingVouchersQuery, IReadOnlyList<VoucherResponse>>
{
    /// <summary>
    /// Cuántas horas mantenemos visibles los vouchers ya decididos.
    /// Sin esto, al confirmar/rechazar el voucher desaparece de inmediato
    /// y la recepcionista pierde la continuidad ("¿lo aprobé bien?").
    /// </summary>
    private static readonly TimeSpan DecidedWindow = TimeSpan.FromHours(6);

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ListPendingVouchersHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<VoucherResponse>>> HandleAsync(
        ListPendingVouchersQuery query, CancellationToken ct)
    {
        var cutoff = _clock.UtcNow - DecidedWindow;

        // Pending siempre + decididos en las últimas 6h (para que la recepcionista
        // vea el rastro reciente y no se asuste si la lista queda vacía después
        // de aprobar un par).
        var vouchers = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .Where(v => v.Status == PaymentVoucherStatus.Pending
                     || (v.DecidedAt != null && v.DecidedAt >= cutoff))
            // Orden:
            //  1. Pending arriba (más urgentes según cita próxima).
            //  2. Decididos abajo (más recién decididos primero).
            .OrderBy(v => v.Status == PaymentVoucherStatus.Pending ? 0 : 1)
            .ThenBy(v => v.Status == PaymentVoucherStatus.Pending
                ? v.Appointment!.StartAt
                : DateTime.MaxValue)
            .ThenByDescending(v => v.DecidedAt)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var responses = vouchers.Select(v => VoucherMapper.ToResponse(v, now)).ToList();

        return Result<IReadOnlyList<VoucherResponse>>.Success(responses);
    }
}
