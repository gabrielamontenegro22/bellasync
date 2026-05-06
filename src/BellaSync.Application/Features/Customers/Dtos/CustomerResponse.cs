namespace BellaSync.Application.Features.Customers.Dtos;

/// <summary>
/// DTO de salida para representar un cliente en respuestas de la API.
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public bool AcceptsMarketing { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
