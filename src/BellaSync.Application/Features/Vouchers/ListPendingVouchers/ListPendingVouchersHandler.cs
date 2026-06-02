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
        var pending = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .Where(v => v.Status == PaymentVoucherStatus.Pending)
            // Ordenar por cita próxima primero (los más urgentes arriba)
            .OrderBy(v => v.Appointment!.StartAt)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var responses = pending.Select(v => VoucherMapper.ToResponse(v, now)).ToList();

        return Result<IReadOnlyList<VoucherResponse>>.Success(responses);
    }
}
