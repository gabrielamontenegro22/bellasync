namespace BellaSync.Domain.Entities;

/// <summary>
/// Decisión sobre el anticipo cuando la cita asociada se cancela.
/// Se setea en PaymentVoucher.RefundDecision al momento de cancelar.
///
/// El valor es null mientras el voucher está activo (cita no cancelada).
/// Una vez la cita se cancela y el voucher estaba Validated, el handler
/// de cancel computa la decisión y la persiste:
///
///   - Refunded:        admin va a transferir la plata de vuelta al cliente.
///                      Aparece en "Devoluciones pendientes" hasta que la
///                      admin confirme la transferencia.
///   - CreditPending:   la cliente reagenda en el momento; el anticipo se
///                      reserva para la nueva cita (no se mueve plata).
///                      Cuando se crea la cita nueva, se "consume" el crédito.
///   - Forfeited:       canceló muy tarde, el salón se queda con la plata.
///                      No requiere acción posterior.
/// </summary>
public enum DepositRefundDecision
{
    Refunded = 0,
    CreditPending = 1,
    Forfeited = 2,
}
