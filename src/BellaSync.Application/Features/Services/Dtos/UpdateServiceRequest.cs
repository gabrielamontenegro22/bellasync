using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Services.Dtos;

/// <summary>
/// Request para editar un servicio existente.
/// Mismo shape que Create + permite cambiar IsActive (reactivar un servicio archivado).
/// </summary>
public class UpdateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ServiceCategory Category { get; set; } = ServiceCategory.Otros;

    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }

    public string? Color { get; set; }

    public bool RequiresDeposit { get; set; } = false;
    public decimal DepositPercentage { get; set; } = 0m;

    public bool IsActive { get; set; } = true;
}
