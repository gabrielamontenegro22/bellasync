namespace BellaSync.Application.Features.Stylists.Dtos;

/// <summary>
/// Request para crear un estilista nuevo en el salón.
/// El TenantId se toma del JWT.
/// </summary>
public class CreateStylistRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Color hex (#RRGGBB) para identificarlo en la agenda. Opcional.</summary>
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Ids de los servicios que el estilista sabe realizar.
    /// Pueden venir vacíos: un estilista nuevo puede no tener servicios asignados todavía.
    /// </summary>
    public List<Guid> ServiceIds { get; set; } = new();
}
