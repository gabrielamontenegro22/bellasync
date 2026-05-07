namespace BellaSync.Application.Features.Stylists.Dtos;

/// <summary>
/// Servicio asignado a un estilista, en versión simplificada para incluir
/// dentro del StylistResponse sin tener que hacer otra llamada al backend.
/// </summary>
public class StylistAssignedServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// DTO de salida para representar un estilista en respuestas de la API.
/// Incluye la lista de servicios que sabe hacer.
/// </summary>
public class StylistResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? IdNumber { get; set; }
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// "Active" / "Vacation" / "Inactive".
    /// Se serializa como string gracias al JsonStringEnumConverter,
    /// pero acá lo declaramos como string para mayor claridad en el contrato.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public List<StylistAssignedServiceDto> Services { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
