using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.CreateProduct;

/// <summary>
/// Crea un producto en el inventario del salón.
///
/// InitialStock es opcional: si viene > 0, el handler además crea
/// automáticamente un ProductMovement tipo Inflow con motivo
/// "Stock inicial" para que el stock arranque cargado Y quede
/// el registro en el historial. Esto evita el flujo de 2 pasos
/// que la admin tenía antes (crear → cerrar → registrar movimiento).
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Brand,
    /// <summary>Id de la categoría del tenant. La admin las gestiona en /inventario.</summary>
    Guid CategoryId,
    int MinStock,
    decimal Cost,
    /// <summary>Cantidad inicial en stock. null o 0 = arranca vacío.</summary>
    int? InitialStock
) : ICommand<ProductResponse>;
