using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Stylists.ListStylists;

public sealed class ListStylistsHandler
    : IQueryHandler<ListStylistsQuery, IReadOnlyList<StylistResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListStylistsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<StylistResponse>>> HandleAsync(
        ListStylistsQuery query, CancellationToken ct)
    {
        var dbQuery = _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service);

        IQueryable<Stylist> filtered = query.IncludeInactive
            ? dbQuery
            : dbQuery.Where(s => s.Status != StylistStatus.Inactive);

        var stylists = await filtered.OrderBy(s => s.FullName).ToListAsync(ct);

        return Result<IReadOnlyList<StylistResponse>>.Success(
            stylists.Select(StylistMapper.ToResponse).ToList());
    }
}
