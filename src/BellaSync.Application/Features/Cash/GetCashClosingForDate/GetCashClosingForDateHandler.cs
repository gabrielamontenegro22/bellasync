using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Cash.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Cash.GetCashClosingForDate;

public sealed class GetCashClosingForDateHandler
    : IQueryHandler<GetCashClosingForDateQuery, CashClosingResponse?>
{
    private readonly IApplicationDbContext _db;

    public GetCashClosingForDateHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CashClosingResponse?>> HandleAsync(
        GetCashClosingForDateQuery query, CancellationToken ct)
    {
        var cc = await _db.CashClosings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClosedDate == query.Date, ct);

        return Result<CashClosingResponse?>.Success(
            cc is null ? null : CashClosingMapper.ToResponse(cc));
    }
}
