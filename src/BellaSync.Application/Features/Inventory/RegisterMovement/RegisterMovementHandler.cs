using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.Shared;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Inventory.RegisterMovement;

/// <summary>
/// Registra un movimiento + actualiza el stock del producto en una sola
/// transacción. El ProductMovement guarda snapshots StockBefore/After
/// para que el historial sea legible aunque cambie la lógica de stock
/// más adelante.
/// </summary>
public sealed class RegisterMovementHandler
    : ICommandHandler<RegisterMovementCommand, ProductMovementResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<RegisterMovementHandler> _logger;

    public RegisterMovementHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<RegisterMovementHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ProductMovementResponse>> HandleAsync(
        RegisterMovementCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<ProductMovementKind>(command.Kind, ignoreCase: true, out var kind))
            return ApplicationError.Validation(
                "movement.invalid_kind",
                "Tipo de movimiento inválido. Usar Inflow, Outflow o Adjustment.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            return ApplicationError.Validation(
                "movement.reason_required",
                "El motivo del movimiento es obligatorio.");

        if (command.Qty <= 0 && kind != ProductMovementKind.Adjustment)
            return ApplicationError.Validation(
                "movement.qty_required",
                "La cantidad debe ser mayor a cero.");

        if (command.Qty < 0)
            return ApplicationError.Validation(
                "movement.qty_negative",
                "La cantidad no puede ser negativa.");

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, ct);

        if (product is null)
            return ApplicationError.NotFound("product.not_found", "Producto no encontrado.");

        var stockBefore = product.Stock;
        var now = _clock.UtcNow;

        try
        {
            switch (kind)
            {
                case ProductMovementKind.Inflow:
                    product.RegisterInflow(command.Qty, now);
                    break;
                case ProductMovementKind.Outflow:
                    product.RegisterOutflow(command.Qty, now);
                    break;
                case ProductMovementKind.Adjustment:
                    product.AdjustTo(command.Qty, now);
                    break;
            }
        }
        catch (DomainException ex)
        {
            // Stock insuficiente o ajuste negativo — error de validación
            // (el frontend muestra el mensaje al user, no es bug).
            return ApplicationError.Validation("movement.domain_invalid", ex.Message);
        }

        ProductMovement movement;
        try
        {
            movement = ProductMovement.Create(
                tenantId: _currentTenant.TenantId,
                productId: product.Id,
                kind: kind,
                qty: command.Qty,
                reason: command.Reason,
                stockBefore: stockBefore,
                stockAfter: product.Stock,
                notes: command.Notes,
                registeredByUserId: command.RegisteredByUserId,
                utcNow: now);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("movement.invalid", ex.Message);
        }

        _db.ProductMovements.Add(movement);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Movimiento {Kind} de {Qty} en producto {ProductId} ({Name}): {Before} → {After}",
            kind, command.Qty, product.Id, product.Name, stockBefore, product.Stock);

        // Re-leer con Include para devolver el nombre del usuario que registró.
        var created = await _db.ProductMovements
            .AsNoTracking()
            .Include(m => m.RegisteredByUser)
            .FirstAsync(m => m.Id == movement.Id, ct);

        return Result<ProductMovementResponse>.Success(InventoryMapper.ToResponse(created));
    }
}
