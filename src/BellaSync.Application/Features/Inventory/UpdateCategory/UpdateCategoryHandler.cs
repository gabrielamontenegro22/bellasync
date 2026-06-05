using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.UpdateCategory;

public sealed class UpdateCategoryHandler
    : ICommandHandler<UpdateCategoryCommand, ProductCategoryResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateCategoryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ProductCategoryResponse>> HandleAsync(
        UpdateCategoryCommand command, CancellationToken ct)
    {
        var category = await _db.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (category is null)
            return ApplicationError.NotFound("category.not_found", "Categoría no encontrada.");

        if (!Enum.TryParse<ProductTone>(command.Tone, ignoreCase: true, out var tone))
            return ApplicationError.Validation("category.invalid_tone", "Color inválido.");

        // Validar duplicado de nombre (excluyendo a sí mismo).
        var trimmed = command.Name?.Trim() ?? string.Empty;
        var dup = await _db.ProductCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id != command.Id && c.Name.ToLower() == trimmed.ToLower(), ct);

        if (dup)
            return ApplicationError.Conflict(
                "category.name_taken",
                $"Ya existe otra categoría llamada \"{trimmed}\".");

        try
        {
            category.Rename(trimmed);
            category.ChangeTone(tone);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("category.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var count = await _db.Products
            .AsNoTracking()
            .CountAsync(p => p.CategoryId == category.Id && p.IsActive, ct);

        return Result<ProductCategoryResponse>.Success(
            InventoryMapper.ToResponse(category, count));
    }
}
