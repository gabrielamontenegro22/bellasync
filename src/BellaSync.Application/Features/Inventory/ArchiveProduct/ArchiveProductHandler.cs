using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.ArchiveProduct;

/// <summary>
/// Toggle archive/reactivate. Idempotente. Los movimientos históricos
/// siguen referenciando al producto aunque esté archivado — no se rompe
/// la trazabilidad.
/// </summary>
public sealed class ArchiveProductHandler : ICommandHandler<ArchiveProductCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public ArchiveProductHandler(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(ArchiveProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct);

        if (product is null)
            return ApplicationError.NotFound("product.not_found", "Producto no encontrado.");

        if (command.Active) product.Reactivate(_clock.UtcNow);
        else product.Archive(_clock.UtcNow);

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
