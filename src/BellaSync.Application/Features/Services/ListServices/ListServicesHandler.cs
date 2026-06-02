using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Application.Features.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Services.ListServices;

public sealed class ListServicesHandler
    : IQueryHandler<ListServicesQuery, IReadOnlyList<ServiceResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListServicesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ServiceResponse>>> HandleAsync(
        ListServicesQuery query, CancellationToken ct)
    {
        var dbQuery = _db.Services.AsNoTracking();
        if (!query.IncludeInactive)
            dbQuery = dbQuery.Where(s => s.IsActive);

        var services = await dbQuery
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ServiceResponse>>.Success(
            services.Select(ServiceMapper.ToResponse).ToList());
    }
}
