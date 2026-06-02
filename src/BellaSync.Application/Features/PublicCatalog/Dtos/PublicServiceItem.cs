namespace BellaSync.Application.Features.PublicCatalog.Dtos;

/// <summary>
/// Servicio visible para el cliente del portal público.
/// Solo expone lo que el cliente necesita decidir si lo quiere agendar.
/// </summary>
public class PublicServiceItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string? Color { get; set; }
    public bool RequiresDeposit { get; set; }
    public decimal DepositPercentage { get; set; }
    /// <summary>Calculado: price × depositPercentage / 100. Para mostrar "anticipo $X".</summary>
    public decimal DepositAmount { get; set; }
}
