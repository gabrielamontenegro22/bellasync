namespace BellaSync.Application.Features.Vouchers.Dtos;

/// <summary>
/// Voucher Validated que quedó con un refund pendiente de resolver luego
/// de que la cita asociada se canceló. La admin lo ve en la sección
/// "Devoluciones pendientes" de Caja y, cuando ya hizo la transferencia
/// bancaria por fuera del sistema, marca el voucher como resuelto.
///
/// Solo aparecen los que tienen RefundDecision = Refunded o
/// CreditPending y RefundResolvedAt = null. Forfeited se autoresuelve
/// en el momento de la cancelación.
/// </summary>
public sealed class PendingRefundResponse
{
    public Guid VoucherId { get; set; }
    public Guid AppointmentId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;

    /// <summary>Cuándo era la cita cancelada (snapshot para mostrar contexto).</summary>
    public DateTime AppointmentStartAt { get; set; }

    /// <summary>Monto del anticipo (ReportedAmount del voucher).</summary>
    public decimal Amount { get; set; }

    /// <summary>Banco que reportó la cliente al enviar el comprobante. Opcional.</summary>
    public string? Bank { get; set; }

    /// <summary>Cuándo se canceló la cita (= cuándo se decidió el refund).</summary>
    public DateTime CancelledAt { get; set; }

    /// <summary>Motivo de la cancelación que escribió quien la canceló.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>"Refunded" / "CreditPending"</summary>
    public string Decision { get; set; } = string.Empty;
}
