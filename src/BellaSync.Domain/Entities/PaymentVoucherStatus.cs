namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del comprobante de pago. Mutar via PaymentVoucher.Confirm/Reject/RequestClarification.
/// </summary>
public enum PaymentVoucherStatus
{
    /// <summary>Recibido (vía WhatsApp en el futuro), esperando que recepción decida.</summary>
    Pending = 0,

    /// <summary>Recepción confirmó: pago válido. La cita pasa a Confirmed.</summary>
    Validated = 1,

    /// <summary>Recepción rechazó el comprobante (no coincide monto/cuenta/etc.).</summary>
    Rejected = 2,

    /// <summary>Recepción pidió aclaración al cliente (nuevo comprobante, etc.).</summary>
    NeedsClarification = 3,
}
