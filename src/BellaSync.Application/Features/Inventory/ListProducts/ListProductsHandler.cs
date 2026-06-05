using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.ListProducts;

public sealed class ListProductsHandler
    : IQueryHandler<ListProductsQuery, IReadOnlyList<ProductResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListProductsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ProductResponse>>> HandleAsync(
        ListProductsQuery query, CancellationToken ct)
    {
        IQueryable<Product> q = _db.Products.AsNoTracking();

        if (!query.IncludeArchived)
            q = q.Where(p => p.IsActive);

        // Filtro de categoría server-side: si CategoryId está seteado, restringe.
        if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
        {
            var catId = query.CategoryId.Value;
            q = q.Where(p => p.CategoryId == catId);
        }

        // Search libre case-insensitive (name OR brand). Usamos ToLower().Contains()
        // que EF traduce a SQL `LOWER(col) LIKE '%needle%'`, portable a cualquier
        // provider (no acoplamos Application a Npgsql vía EF.Functions.ILike).
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var needle = query.Query.Trim().ToLower();
            q = q.Where(p =>
                p.Name.ToLower().Contains(needle) ||
                p.Brand.ToLower().Contains(needle));
        }

        // Include al final — un solo lugar, para hidratar Tone+Name del DTO.
        var list = await q
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        // Status se calcula in-memory porque depende del ratio stock/min y
        // queremos consistencia con el helper del mockup (no portarlo a SQL).
        // El filtro de status se aplica acá también.
        var dtos = list.Select(InventoryMapper.ToResponse);
        if (!string.IsNullOrWhiteSpace(query.Status)
            && !query.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Convención: el chip "OK" del mockup incluye "ok" + "warn"
            // (ambos colores verde/limítrofe se consideran "saludable").
            var wanted = query.Status.Trim().ToLowerInvariant();
            dtos = wanted switch
            {
                "ok"  => dtos.Where(d => d.Status == "ok" || d.Status == "warn"),
                "low" => dtos.Where(d => d.Status == "low"),
                "out" => dtos.Where(d => d.Status == "out"),
                _     => dtos,
            };
        }

        return Result<IReadOnlyList<ProductResponse>>.Success(dtos.ToList());
    }
}
