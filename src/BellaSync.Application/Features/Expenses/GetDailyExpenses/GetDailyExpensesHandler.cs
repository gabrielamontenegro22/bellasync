using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Application.Features.Expenses.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Expenses.GetDailyExpenses;

public sealed class GetDailyExpensesHandler
    : IQueryHandler<GetDailyExpensesQuery, IReadOnlyList<ExpenseResponse>>
{
    // BellaSync opera solo en Colombia (UTC-5 todo el año).
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public GetDailyExpensesHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<ExpenseResponse>>> HandleAsync(
        GetDailyExpensesQuery query, CancellationToken ct)
    {
        var date = query.Date ?? DateOnly.FromDateTime(
            _clock.UtcNow.Add(ColombiaOffset));

        var dayStartUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ColombiaOffset)
            .UtcDateTime;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.RegisteredAt >= dayStartUtc && e.RegisteredAt < dayEndUtc)
            .OrderBy(e => e.RegisteredAt)
            .ToListAsync(ct);

        IReadOnlyList<ExpenseResponse> items = expenses
            .Select(ExpenseMapper.ToResponse)
            .ToList();

        return Result<IReadOnlyList<ExpenseResponse>>.Success(items);
    }
}
