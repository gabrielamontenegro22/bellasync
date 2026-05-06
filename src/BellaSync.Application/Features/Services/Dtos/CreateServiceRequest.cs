using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Services.Dtos;

/// <summary>
/// Request para crear un servicio nuevo en el catálogo del salón.
/// El TenantId no viaja en el body — se toma del JWT del usuario autenticado.
/// </summary>
public class CreateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ServiceCategory Category { get; set; } = ServiceCategory.Otros;

    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }

    /// <summary>Color hex en formato #RRGGBB (ej. "#1f5d50"). Opcional.</summary>
    public string? Color { get; set; }
}
