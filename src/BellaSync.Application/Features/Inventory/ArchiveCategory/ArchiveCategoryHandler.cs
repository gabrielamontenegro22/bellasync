using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.ArchiveCategory;

/// <summary>
/// Archivar/reactivar una categoría. Si hay productos ACTIVOS asignados,
/// el archivado se bloquea — la admin debe re-categorizar primero. Esto
/// evita huérfanos visuales (productos activos apuntando a una categoría
/// "tachada" en la UI).
/// </summary>
public sealed class ArchiveCategoryHandler : ICommandHandler<ArchiveCategoryCommand>
{
    private readonly IApplicationDbContext _db;

    public ArchiveCategoryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> HandleAsync(ArchiveCategoryCommand command, CancellationToken ct)
    {
        var category = await _db.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (category is null)
            return ApplicationError.NotFound("category.not_found", "Categoría no encontrada.");

        if (!command.Active)
        {
            // Archivar: chequea productos activos.
            var hasActiveProducts = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.CategoryId == command.Id && p.IsActive, ct);

            if (hasActiveProducts)
                return ApplicationError.Conflict(
                    "category.has_active_products",
                    $"No se puede archivar \"{category.Name}\" porque tiene productos activos. " +
                    "Re-categorizalos primero o archivalos.");

            category.Archive();
        }
        else
        {
            category.Reactivate();
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
