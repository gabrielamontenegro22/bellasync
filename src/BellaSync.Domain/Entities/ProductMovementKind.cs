namespace BellaSync.Domain.Entities;

/// <summary>
/// Tipo de movimiento de inventario. Define el signo del impacto sobre el stock:
///   - Inflow  → suma (compra al proveedor, devolución de cliente, etc.)
///   - Outflow → resta (consumo, merma, devolución a proveedor)
///   - Adjustment → setea el stock a un valor absoluto (corrección, inventario físico)
///
/// Los movimientos son inmutables — para "deshacer" se registra el inverso.
/// Esto deja trazabilidad completa: el historial de un producto cuenta toda
/// su vida (cuándo entró, cuándo salió, por qué).
/// </summary>
public enum ProductMovementKind
{
    Inflow = 0,
    Outflow = 1,
    Adjustment = 2,
}
