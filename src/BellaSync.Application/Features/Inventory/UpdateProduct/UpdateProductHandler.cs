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

namespace BellaSync.Application.Features.Inventory.UpdateProduct;

public sealed class UpdateProductHandler
    : ICommandHandler<UpdateProductCommand, ProductResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateProductHandler> _logger;

    public UpdateProductHandler(
        IApplicationDbContext db,
        IClock clock,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        ILogger<UpdateProductHandler> logger)
    {
        _db = db;
        _clock = clock;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<ProductResponse>> HandleAsync(
        UpdateProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct);

        if (product is null)
            return ApplicationError.NotFound("product.not_found", "Producto no encontrado.");

        // Validamos categoría destino: existe en el tenant y está activa.
        var category = await _db.ProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId, ct);

        if (category is null)
            return ApplicationError.Validation(
                "product.invalid_category",
                "La categoría seleccionada no existe en este salón.");

        if (!category.IsActive)
            return ApplicationError.Validation(
                "product.archived_category",
                "La categoría está archivada. Elegí otra activa.");

        Money cost;
        try { cost = Money.Create(command.Cost); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid_cost", ex.Message);
        }

        // 1) Aplicar los cambios de datos básicos (nombre, marca, categoría,
        //    unidad, mínimo, costo). NO toca stock.
        try
        {
            product.UpdateDetails(
                command.Name, command.Brand, command.CategoryId,
                command.MinStock, cost, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("product.invalid", ex.Message);
        }

        // 2) Si se mandó NewStock distinto del actual, ajustar + crear
        //    movimiento de auditoría. Esto soluciona el caso típico:
        //    "hice inventario físico, conté 20 en vez de 25" — la admin
        //    cambia el stock acá y el sistema deja registro automático.
        var stockBefore = product.Stock;
        if (command.NewStock.HasValue && command.NewStock.Value != stockBefore)
        {
            if (command.NewStock.Value < 0)
                return ApplicationError.Validation(
                    "product.stock_negative",
                    "El stock no puede ser negativo.");

            try
            {
                product.AdjustTo(command.NewStock.Value, _clock.UtcNow);
            }
            catch (DomainException ex)
            {
                return ApplicationError.Validation("product.invalid_stock", ex.Message);
            }

            // Crear el movimiento de auditoría. Reason explica que vino del
            // form de edición (vs un Ajuste explícito desde el modal de mov).
            // Qty = newStock (convención para Adjustment: el valor final, no el delta).
            var auditMovement = ProductMovement.Create(
                tenantId: _currentTenant.TenantId,
                productId: product.Id,
                kind: ProductMovementKind.Adjustment,
                qty: command.NewStock.Value,
                reason: "Ajuste desde editar producto",
                stockBefore: stockBefore,
                stockAfter: product.Stock,
                notes: $"Cambio manual de stock vía form de edición ({stockBefore} → {product.Stock}).",
                registeredByUserId: _currentUser.UserId,
                utcNow: _clock.UtcNow);

            _db.ProductMovements.Add(auditMovement);

            _logger.LogInformation(
                "Stock de producto {ProductId} ({Name}) cambiado desde form: {Before} → {After}",
                product.Id, product.Name, stockBefore, product.Stock);
        }

        await _db.SaveChangesAsync(ct);

        // Re-leer con la nueva categoría adjunta.
        var fresh = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstAsync(p => p.Id == product.Id, ct);

        return Result<ProductResponse>.Success(InventoryMapper.ToResponse(fresh));
    }
}
