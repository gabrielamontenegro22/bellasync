using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.PublicCatalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.PublicCatalog.ListPublicServices;

public sealed class ListPublicServicesHandler
    : IQueryHandler<ListPublicServicesQuery, IReadOnlyList<PublicServiceItem>>
{
    private readonly IApplicationDbContext _db;

    public ListPublicServicesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PublicServiceItem>>> HandleAsync(
        ListPublicServicesQuery query, CancellationToken ct)
    {
        // Endpoint anónimo: NO hay tenant en JWT. IgnoreQueryFilters + filtrar
        // por slug manualmente.
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == query.TenantSlug && t.IsActive, ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "El salón no existe.");

        var services = await _db.Services
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.Category).ThenBy(s => s.Name)
            .ToListAsync(ct);

        var items = services.Select(s => new PublicServiceItem
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Category = s.Category.ToString(),
            DurationMinutes = s.DurationMinutes,
            Price = s.Price.Amount,
            Color = s.Color,
            RequiresDeposit = s.RequiresDeposit,
            DepositPercentage = s.DepositPercentage.Value,
            DepositAmount = s.RequiresDeposit
                ? Math.Round(s.Price.Amount * s.DepositPercentage.Value / 100m, 2)
                : 0m,
        }).ToList();

        return Result<IReadOnlyList<PublicServiceItem>>.Success(items);
    }
}
