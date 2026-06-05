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
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<CreateProductHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
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
                minStock: command.MinStock,
                cost: cost,
                utcNow: _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        _db.Products.Add(product);

        // Si la admin indicó stock inicial > 0 al crear, registramos la
        // entrada automáticamente. Misma transacción que el create del
        // producto — o ambos commitean o ninguno (consistency).
        // El movimiento queda en el historial con motivo claro, así si
        // después la admin abre "Ver historial" ve el origen del stock.
        if (command.InitialStock.HasValue && command.InitialStock.Value > 0)
        {
            var qty = command.InitialStock.Value;
            try { product.RegisterInflow(qty, _clock.UtcNow); }
            catch (DomainException ex)
            {
                return ApplicationError.Validation("product.invalid_initial_stock", ex.Message);
            }

            ProductMovement seedMovement;
            try
            {
                seedMovement = ProductMovement.Create(
                    tenantId: _currentTenant.TenantId,
                    productId: product.Id,
                    kind: ProductMovementKind.Inflow,
                    qty: qty,
                    reason: "Stock inicial",
                    stockBefore: 0,
                    stockAfter: product.Stock,
                    notes: "Carga inicial al crear el producto.",
                    registeredByUserId: _currentUser.UserId,
                    utcNow: _clock.UtcNow);
            }
            catch (DomainException ex)
            {
                return ApplicationError.Validation("product.seed_movement_failed", ex.Message);
            }
            _db.ProductMovements.Add(seedMovement);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Producto creado {ProductId}: {Name} ({Brand}) en categoría {CategoryName}, stock inicial {Stock}",
            product.Id, product.Name, product.Brand, category.Name, product.Stock);

        // Re-leer con la categoría adjunta para hidratar nombre+tono del DTO.
        var fresh = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstAsync(p => p.Id == product.Id, ct);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(fresh));
    }
}
