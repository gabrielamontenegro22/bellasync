namespace BellaSync.Domain.Entities;

/// <summary>
/// Categorías de productos del inventario del salón.
/// Espeja los chips de filtro del mockup. Strings de visualización
/// se traducen en frontend; en backend usamos los nombres del enum.
/// </summary>
public enum ProductCategory
{
    /// <summary>Tintes, decolorantes, oxidantes, shampoos profesionales, mascarillas.</summary>
    Hair = 0,

    /// <summary>Esmaltes, removedores, semipermanentes, gel, etc.</summary>
    Nails = 1,

    /// <summary>Cera, bandas, postdepilación, etc.</summary>
    Hairremoval = 2,

    /// <summary>Cremas reductoras, aceites de masaje, aromaterapia.</summary>
    Spa = 3,

    /// <summary>Algodón, toallas, capas, bandas — fungibles que no son de un servicio puntual.</summary>
    Accessories = 4,
}

/// <summary>
/// Color visual del avatar del producto en la tabla. Decisión cosmética
/// que se setea al crear un producto (default según categoría). El usuario
/// puede sobrescribir desde el form para personalizar.
/// </summary>
public enum ProductTone
{
    Rose = 0,
    Amber = 1,
    Sand = 2,
    Olive = 3,
    Wine = 4,
    Mist = 5,
}
