namespace BellaSync.Application.Features.Services.Dtos;

/// <summary>
/// DTO de salida para representar un servicio en respuestas de la API.
/// Convierte el enum ServiceCategory a string para que el frontend lo lea
/// como "Cabello", "Unas", etc. en lugar de un número.
/// </summary>
public class ServiceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal CommissionPercentage { get; set; }

    public string? Color { get; set; }
    public bool IsActive { get; set; }

    public bool RequiresDeposit { get; set; }
    public decimal DepositPercentage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
