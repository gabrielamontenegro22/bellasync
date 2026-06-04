namespace BellaSync.Application.Features.Commissions.Dtos;

/// <summary>
/// Fila de la tabla "Historial de liquidaciones".
/// </summary>
public class CommissionPayoutResponse
{
    public Guid Id { get; set; }
    public Guid StylistId { get; set; }
    public string StylistName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>YYYY-MM-DD.</summary>
    public string PeriodFrom { get; set; } = string.Empty;
    public string PeriodTo { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public Guid? PaidByUserId { get; set; }
    public string? Notes { get; set; }
}
