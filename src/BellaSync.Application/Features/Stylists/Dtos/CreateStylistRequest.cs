namespace BellaSync.Application.Features.Stylists.Dtos;

/// <summary>
/// Request para crear un estilista nuevo en el salón.
/// El TenantId se toma del JWT.
/// El estado siempre arranca como Active (no se acepta en este request).
/// </summary>
public class CreateStylistRequest
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Cargo: "Estilista", "Estilista Senior", "Colorista", etc.</summary>
    public string Role { get; set; } = "Estilista";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Cédula colombiana, almacenada tal cual la escribe la admin.</summary>
    public string? IdNumber { get; set; }

    /// <summary>Color hex (#RRGGBB) para identificarlo en la agenda. Opcional.</summary>
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Ids de los servicios que el estilista sabe realizar.
    /// Pueden venir vacíos: un estilista nuevo puede no tener servicios asignados todavía.
    /// </summary>
    public List<Guid> ServiceIds { get; set; } = new();
}
