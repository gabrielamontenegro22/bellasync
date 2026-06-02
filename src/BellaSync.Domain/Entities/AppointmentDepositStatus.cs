namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del anticipo de una cita.
///
/// - NotRequired: el servicio no exige anticipo. La cita pasa directo a
///   Confirmed al crearse.
/// - AwaitingPayment: el cliente debe transferir y enviar comprobante por
///   WhatsApp. La cita queda en Pending con HoldExpiresAt seteado.
/// - Validated: la recepción validó el voucher. Confirm() puede transicionar
///   la cita a Confirmed.
/// </summary>
public enum AppointmentDepositStatus
{
    NotRequired = 0,
    AwaitingPayment = 1,
    Validated = 2,
}
