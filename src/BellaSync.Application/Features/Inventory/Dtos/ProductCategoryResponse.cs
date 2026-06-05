namespace BellaSync.Application.Features.Inventory.Dtos;

/// <summary>
/// Categoría del catálogo de inventario del salón.
/// productsCount es derivado para que el frontend pueda mostrar "(12)"
/// al lado del nombre y bloquear el archivado si tiene productos activos.
/// </summary>
public sealed class ProductCategoryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>"Rose" | "Amber" | "Sand" | "Olive" | "Wine" | "Mist"</summary>
    public string Tone { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    /// <summary>Productos ACTIVOS asignados a esta categoría. Para UI/validación.</summary>
    public int ActiveProductsCount { get; init; }
}
