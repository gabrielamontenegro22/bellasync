namespace BellaSync.Application.Features.Vouchers.Dtos;

/// <summary>DTO de salida para un voucher en la cola de validación.</summary>
public class VoucherResponse
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }

    // Snapshot del cliente y servicio (denormalizado para la UI de cola)
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;

    public DateTime AppointmentStartAt { get; set; }
    public decimal AppointmentDepositAmount { get; set; }   // lo que la cita exige
    /// <summary>
    /// Precio total del servicio de la cita (PriceSnapshot). Lo expone
    /// para que la UI muestre "X% anticipo de $Y total" en la card de
    /// Pago esperado.
    /// </summary>
    public decimal AppointmentTotalServicePrice { get; set; }
    public decimal ReportedAmount { get; set; }              // lo que el cliente envió

    public string? Bank { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? ImageUrl { get; set; }

    public DateTime ReceivedAt { get; set; }

    /// <summary>"Pending" / "Validated" / "Rejected" / "NeedsClarification"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Calculado en el handler según hora hasta la cita:
    /// "urgent" (≤ 6h), "tomorrow" (≤ 36h), "week" (>36h).
    /// </summary>
    public string Urgency { get; set; } = string.Empty;

    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
}
