using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.PublicCatalog.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.PublicCatalog.ListPublicStylists;

public sealed class ListPublicStylistsHandler
    : IQueryHandler<ListPublicStylistsQuery, IReadOnlyList<PublicStylistItem>>
{
    private readonly IApplicationDbContext _db;

    public ListPublicStylistsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PublicStylistItem>>> HandleAsync(
        ListPublicStylistsQuery query, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == query.TenantSlug && t.IsActive, ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "El salón no existe.");

        var stylists = await _db.Stylists
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.StylistServices)
            .Where(s => s.TenantId == tenant.Id && s.Status != StylistStatus.Inactive)
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);

        var items = stylists.Select(s => new PublicStylistItem
        {
            Id = s.Id,
            FullName = s.FullName,
            Role = s.Role,
            Color = s.Color,
            ServiceIds = s.StylistServices.Select(ss => ss.ServiceId).ToList(),
        }).ToList();

        return Result<IReadOnlyList<PublicStylistItem>>.Success(items);
    }
}
