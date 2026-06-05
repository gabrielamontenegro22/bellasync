using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.ListMovements;

public sealed class ListMovementsHandler
    : IQueryHandler<ListMovementsQuery, IReadOnlyList<ProductMovementResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListMovementsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ProductMovementResponse>>> HandleAsync(
        ListMovementsQuery query, CancellationToken ct)
    {
        var list = await _db.ProductMovements
            .AsNoTracking()
            .Include(m => m.RegisteredByUser)
            .Where(m => m.ProductId == query.ProductId)
            .OrderByDescending(m => m.RegisteredAt)
            .Take(200)  // cap defensivo — productos con mucho movimiento
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProductMovementResponse>>.Success(
            list.Select(InventoryMapper.ToResponse).ToList());
    }
}
