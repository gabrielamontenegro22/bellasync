namespace BellaSync.Application.Features.Stylists.TimeOff.Dtos;

/// <summary>Período de no-disponibilidad de un estilista.</summary>
public sealed class StylistTimeOffResponse
{
    public Guid Id { get; init; }
    public Guid StylistId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string? Reason { get; init; }
    /// <summary>True si el período ya pasó completo (ToDate &lt; hoy).</summary>
    public bool IsPast { get; init; }
    public DateTime CreatedAt { get; init; }
}
