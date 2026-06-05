using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
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
        if (!Enum.TryParse<ProductCategory>(command.Category, ignoreCase: true, out var cat))
            return ApplicationError.Validation(
                "product.invalid_category",
                "Categoría inválida. Usar: Hair, Nails, Hairremoval, Spa, Accessories.");

        // Default de tono por categoría (espeja la convención visual del
        // mockup donde cada categoría tiene un color predominante).
        var tone = ResolveTone(command.Tone, cat);

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
                category: cat,
                unit: command.Unit,
                minStock: command.MinStock,
                cost: cost,
                tone: tone,
                utcNow: _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Producto creado {ProductId}: {Name} ({Brand}) en tenant {TenantId}",
            product.Id, product.Name, product.Brand, _currentTenant.TenantId);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(product));
    }

    /// <summary>
    /// Si el usuario eligió un tone, lo respetamos. Si no, asignamos uno
    /// según la categoría — coherente con el look del mockup.
    /// </summary>
    private static ProductTone ResolveTone(string? raw, ProductCategory cat)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse<ProductTone>(raw, ignoreCase: true, out var explicitTone))
            return explicitTone;

        return cat switch
        {
            ProductCategory.Hair => ProductTone.Amber,
            ProductCategory.Nails => ProductTone.Rose,
            ProductCategory.Hairremoval => ProductTone.Sand,
            ProductCategory.Spa => ProductTone.Wine,
            ProductCategory.Accessories => ProductTone.Mist,
            _ => ProductTone.Olive,
        };
    }
}
