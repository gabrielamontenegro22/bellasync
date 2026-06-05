using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Inventory.CreateProduct;

public sealed class CreateProductHandler
    : ICommandHandler<CreateProductCommand, ProductResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<CreateProductHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ProductResponse>> HandleAsync(
        CreateProductCommand command, CancellationToken ct)
    {
        // La categoría tiene que existir, pertenecer al tenant actual
        // (filtro global la limita) y estar activa.
        var category = await _db.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId, ct);

        if (category is null)
            return ApplicationError.Validation(
                "product.invalid_category",
                "La categoría seleccionada no existe en este salón.");

        if (!category.IsActive)
            return ApplicationError.Validation(
                "product.archived_category",
                "La categoría está archivada. Reactivala o elegí otra.");

        Money cost;
        try { cost = Money.Create(command.Cost); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid_cost", ex.Message);
        }

        Product product;
        try
        {
            product = Product.Create(
                tenantId: _currentTenant.TenantId,
                name: command.Name,
                brand: command.Brand,
                categoryId: command.CategoryId,
                unit: command.Unit,
                minStock: command.MinStock,
                cost: cost,
                utcNow: _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Producto creado {ProductId}: {Name} ({Brand}) en categoría {CategoryName}",
            product.Id, product.Name, product.Brand, category.Name);

        // Re-leer con la categoría adjunta para hidratar nombre+tono del DTO.
        var fresh = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstAsync(p => p.Id == product.Id, ct);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(fresh));
    }
}
