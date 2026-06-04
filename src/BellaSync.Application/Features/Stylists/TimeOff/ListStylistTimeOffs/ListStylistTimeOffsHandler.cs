using BellaSync.Application.Common;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Stylists.TimeOff.ListStylistTimeOffs;

public sealed class ListStylistTimeOffsHandler
    : IQueryHandler<ListStylistTimeOffsQuery, IReadOnlyList<StylistTimeOffResponse>>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ListStylistTimeOffsHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<StylistTimeOffResponse>>> HandleAsync(
        ListStylistTimeOffsQuery query, CancellationToken ct)
    {
        var today = ColombiaTime.TodayFor(_clock.UtcNow);

        // Cortamos a los últimos 90 días — la admin no necesita ver
        // vacaciones de hace 2 años. Si en el futuro quiere histórico
        // completo, agregamos un endpoint /history.
        var cutoff = today.AddDays(-90);

        var items = await _db.StylistTimeOffs
            .AsNoTracking()
            .Where(t => t.StylistId == query.StylistId && t.ToDate >= cutoff)
            .OrderBy(t => t.FromDate)
            .Select(t => new StylistTimeOffResponse
            {
                Id = t.Id,
                StylistId = t.StylistId,
                FromDate = t.FromDate,
                ToDate = t.ToDate,
                Reason = t.Reason,
                IsPast = t.ToDate < today,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<StylistTimeOffResponse>>.Success(items);
    }
}
