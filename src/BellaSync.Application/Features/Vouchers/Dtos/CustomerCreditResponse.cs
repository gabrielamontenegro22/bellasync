namespace BellaSync.Application.Features.Vouchers.Dtos;

/// <summary>
/// Crédito disponible de un cliente — anticipo que pagó en una cita
/// cancelada y todavía no aplicó a una nueva. Pensado para el modal
/// "Nueva cita" que detecta automáticamente si la cliente tiene crédito
/// y ofrece aplicarlo.
///
/// Cada item corresponde a 1 voucher con saldo disponible. Una cliente
/// puede tener varios (canceló varias citas), por eso el endpoint
/// devuelve una lista.
/// </summary>
public sealed class CustomerCreditResponse
{
    /// <summary>Voucher origen del crédito. Se pasa al CreateAppointment.</summary>
    public Guid VoucherId { get; set; }

    /// <summary>Crédito disponible ahora (puede ser saldo parcial si ya se aplicó algo).</summary>
    public decimal AvailableAmount { get; set; }

    /// <summary>Monto original transferido por el cliente (display only).</summary>
    public decimal OriginalAmount { get; set; }

    /// <summary>Servicio de la cita cancelada que generó este crédito (display only).</summary>
    public string OriginalServiceName { get; set; } = string.Empty;

    /// <summary>Cuándo era la cita cancelada (display only — "crédito del 12 de junio").</summary>
    public DateTime OriginalAppointmentDate { get; set; }

    /// <summary>Cuándo se canceló la cita (= cuándo se generó el crédito).</summary>
    public DateTime GeneratedAt { get; set; }
}
