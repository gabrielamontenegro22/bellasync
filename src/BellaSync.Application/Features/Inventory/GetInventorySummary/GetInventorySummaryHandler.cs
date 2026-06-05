using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.GetInventorySummary;

public sealed class GetInventorySummaryHandler
    : IQueryHandler<GetInventorySummaryQuery, InventorySummaryResponse>
{
    private readonly IApplicationDbContext _db;

    public GetInventorySummaryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<InventorySummaryResponse>> HandleAsync(
        GetInventorySummaryQuery query, CancellationToken ct)
    {
        // Una sola query a la BD: traemos los campos necesarios y agrupamos
        // in-memory. Para tenants de salones (~50-200 productos típicamente)
        // el costo es trivial y el código queda legible. Si crece, refactor
        // a un single GROUP BY SQL.
        var rows = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new { p.Stock, p.MinStock, p.Cost })
            .ToListAsync(ct);

        var total = rows.Count;
        var totalValue = rows.Sum(r => r.Stock * r.Cost.Amount);

        int ok = 0, low = 0, outOf = 0;
        foreach (var r in rows)
        {
            var s = InventoryMapper.StatusOf(r.Stock, r.MinStock);
            if (s == "ok" || s == "warn") ok++;
            else if (s == "low") low++;
            else if (s == "out") outOf++;
        }

        return Result<InventorySummaryResponse>.Success(new InventorySummaryResponse
        {
            TotalProducts = total,
            TotalValueCop = totalValue,
            OkCount = ok,
            LowStockCount = low,
            OutOfStockCount = outOf,
        });
    }
}
