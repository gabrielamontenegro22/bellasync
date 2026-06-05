using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.ListCategories;

public sealed class ListCategoriesHandler
    : IQueryHandler<ListCategoriesQuery, IReadOnlyList<ProductCategoryResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListCategoriesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ProductCategoryResponse>>> HandleAsync(
        ListCategoriesQuery query, CancellationToken ct)
    {
        var q = _db.ProductCategories.AsNoTracking();
        if (!query.IncludeArchived) q = q.Where(c => c.IsActive);

        var categories = await q.OrderBy(c => c.Name).ToListAsync(ct);

        // Count de productos activos por categoría en una sola query.
        // Lo cruzamos en memoria — para tenants normales (~10 categorías,
        // ~50-200 productos) es trivial vs 1 query agregada por categoría.
        var counts = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        var dtos = categories
            .Select(c => InventoryMapper.ToResponse(c, counts.GetValueOrDefault(c.Id, 0)))
            .ToList();

        return Result<IReadOnlyList<ProductCategoryResponse>>.Success(dtos);
    }
}
