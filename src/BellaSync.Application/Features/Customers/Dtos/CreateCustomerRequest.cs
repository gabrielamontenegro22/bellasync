namespace BellaSync.Application.Features.Customers.Dtos;

/// <summary>
/// Request para crear un cliente nuevo. El TenantId se toma del JWT.
/// </summary>
public class CreateCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public bool AcceptsMarketing { get; set; } = false;
}
