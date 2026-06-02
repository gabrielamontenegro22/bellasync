namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del ciclo de vida de una cita.
///
/// Transiciones legales (enforzadas en métodos verbales de Appointment):
///
///   Pending     ──Confirm()──→     Confirmed
///   Pending     ──Cancel()───→     Cancelled
///   Confirmed   ──MarkInProgress→  InProgress
///   Confirmed   ──Cancel()───→     Cancelled
///   Confirmed   ──MarkNoShow()─→   NoShow
///   InProgress  ──Complete()──→    Completed
///
/// Estados terminales (sin salida): Completed, Cancelled, NoShow.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>
    /// Solicitada, esperando confirmación. Si requiere anticipo, la cita
    /// está en este estado hasta que el voucher sea validado (DepositStatus
    /// pasa a Validated y entonces se llama Confirm()).
    /// </summary>
    Pending = 0,

    /// <summary>Confirmada, el cupo está reservado y el cliente vendrá.</summary>
    Confirmed = 1,

    /// <summary>El estilista marcó que la cita comenzó.</summary>
    InProgress = 2,

    /// <summary>Cita finalizada exitosamente.</summary>
    Completed = 3,

    /// <summary>Cancelada (por recepción o por hold expiration automático).</summary>
    Cancelled = 4,

    /// <summary>El cliente no se presentó.</summary>
    NoShow = 5,
}
