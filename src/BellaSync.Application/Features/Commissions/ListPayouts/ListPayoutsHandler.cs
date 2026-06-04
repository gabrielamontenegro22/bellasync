using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Commissions.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Commissions.ListPayouts;

public sealed class ListPayoutsHandler
    : IQueryHandler<ListPayoutsQuery, IReadOnlyList<CommissionPayoutResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListPayoutsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CommissionPayoutResponse>>> HandleAsync(
        ListPayoutsQuery query, CancellationToken ct)
    {
        var q = _db.CommissionPayouts
            .AsNoTracking()
            .Include(cp => cp.Stylist)
            .AsQueryable();

        if (query.From.HasValue)
            q = q.Where(cp => cp.PeriodTo >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(cp => cp.PeriodFrom <= query.To.Value);

        var rows = await q
            .OrderByDescending(cp => cp.PaidAt)
            .Take(100)
            .ToListAsync(ct);

        IReadOnlyList<CommissionPayoutResponse> items = rows
            .Select(cp => new CommissionPayoutResponse
            {
                Id = cp.Id,
                StylistId = cp.StylistId,
                StylistName = cp.Stylist?.FullName ?? "—",
                Amount = cp.Amount.Amount,
                PeriodFrom = cp.PeriodFrom.ToString("yyyy-MM-dd"),
                PeriodTo = cp.PeriodTo.ToString("yyyy-MM-dd"),
                PaidAt = cp.PaidAt,
                PaidByUserId = cp.PaidByUserId,
                Notes = cp.Notes,
            })
            .ToList();

        return Result<IReadOnlyList<CommissionPayoutResponse>>.Success(items);
    }
}
