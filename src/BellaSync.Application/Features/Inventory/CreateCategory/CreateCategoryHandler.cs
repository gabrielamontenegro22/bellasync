using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Inventory.CreateCategory;

public sealed class CreateCategoryHandler
    : ICommandHandler<CreateCategoryCommand, ProductCategoryResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public CreateCategoryHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ProductCategoryResponse>> HandleAsync(
        CreateCategoryCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<ProductTone>(command.Tone, ignoreCase: true, out var tone))
            return ApplicationError.Validation(
                "category.invalid_tone",
                "Color inválido. Usar Rose, Amber, Sand, Olive, Wine o Mist.");

        var trimmed = command.Name?.Trim() ?? string.Empty;

        // Dup check case-insensitive (el unique index de la BD lo cubre con
        // case-sensitive, pero acá damos un mensaje accionable).
        var exists = await _db.ProductCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == trimmed.ToLower(), ct);

        if (exists)
            return ApplicationError.Conflict(
                "category.name_taken",
                $"Ya existe una categoría llamada \"{trimmed}\" en este salón.");

        ProductCategory category;
        try
        {
            category = ProductCategory.Create(
                tenantId: _currentTenant.TenantId,
                name: trimmed,
                tone: tone);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("category.invalid", ex.Message);
        }

        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);

        return Result<ProductCategoryResponse>.Success(
            InventoryMapper.ToResponse(category, 0));
    }
}
