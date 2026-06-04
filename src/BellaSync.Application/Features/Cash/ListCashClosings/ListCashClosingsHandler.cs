using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Cash.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Cash.ListCashClosings;

public sealed class ListCashClosingsHandler
    : IQueryHandler<ListCashClosingsQuery, IReadOnlyList<CashClosingResponse>>
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ListCashClosingsHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CashClosingResponse>>> HandleAsync(
        ListCashClosingsQuery query, CancellationToken ct)
    {
        var todayCO = DateOnly.FromDateTime(_clock.UtcNow.Add(ColombiaOffset));
        var from = query.From ?? todayCO.AddDays(-30);
        var to = query.To ?? todayCO;

        var rows = await _db.CashClosings
            .AsNoTracking()
            .Where(cc => cc.ClosedDate >= from && cc.ClosedDate <= to)
            .OrderByDescending(cc => cc.ClosedDate)
            .Take(100)
            .ToListAsync(ct);

        IReadOnlyList<CashClosingResponse> items = rows
            .Select(CashClosingMapper.ToResponse)
            .ToList();

        return Result<IReadOnlyList<CashClosingResponse>>.Success(items);
    }
}
