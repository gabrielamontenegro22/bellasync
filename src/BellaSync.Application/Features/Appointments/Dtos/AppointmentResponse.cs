namespace BellaSync.Application.Features.Appointments.Dtos;

/// <summary>DTO de salida para representar una cita en respuestas de la API.</summary>
public class AppointmentResponse
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public Guid StylistId { get; set; }
    public string StylistName { get; set; } = string.Empty;
    public string? StylistColor { get; set; }

    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? ServiceColor { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public decimal PriceSnapshot { get; set; }
    public decimal DepositPercentage { get; set; }
    public decimal DepositAmount { get; set; }

    /// <summary>"Pending" / "Confirmed" / "InProgress" / "Completed" / "Cancelled" / "NoShow"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>"NotRequired" / "AwaitingPayment" / "Validated"</summary>
    public string DepositStatus { get; set; } = string.Empty;

    /// <summary>"Reception" / "PublicPortal"</summary>
    public string Channel { get; set; } = string.Empty;

    public DateTime? HoldExpiresAt { get; set; }
    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
