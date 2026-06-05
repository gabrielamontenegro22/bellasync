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

    /// <summary>
    /// Suma de los vouchers Confirmed (anticipos online validados por la
    /// recepción) para esta cita. Es el monto REAL que entró al banco
    /// por anticipo — puede coincidir con DepositAmount o no.
    ///
    /// Usado por:
    ///   - El modal "Registrar pago" para pre-rellenar (PriceSnapshot - este).
    ///   - El panel detalle para mostrar "Total / Anticipo / Falta cobrar".
    /// </summary>
    public decimal ValidatedDepositAmount { get; set; }

    /// <summary>
    /// Suma de los Payment registrados directamente para esta cita
    /// (cobros en sitio: efectivo, tarjeta, transferencia manual…).
    /// NO incluye anticipos online — esos están en
    /// <see cref="ValidatedDepositAmount"/>.
    ///
    /// Sirve al modal de cancelar para saber si la cita tiene dinero
    /// asociado y exigir motivo obligatorio sin esperar el rechazo del
    /// backend.
    /// </summary>
    public decimal DirectPaymentsTotal { get; set; }

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
    /// <summary>
    /// Nombre del user que canceló ("María González" / "Sofía Pérez").
    /// Null para cancelaciones automáticas (hold expirado, voucher rechazado
    /// por backend job, etc.) o para citas viejas anteriores a este campo.
    /// La UI lo muestra como "Cancelado por X" en el DetailPanel del agenda.
    /// </summary>
    public string? CancelledByUserName { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
