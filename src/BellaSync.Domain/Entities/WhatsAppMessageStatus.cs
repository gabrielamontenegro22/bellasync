namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado de un mensaje WhatsApp encolado.
///
///   Queued    : armado y guardado, esperando que el dispatcher lo agarre.
///   Sent      : el adapter (NoOp/Twilio/Meta) lo aceptó y lo despachó.
///   Failed    : el adapter rechazó (error de red, número inválido, etc.).
///               Tiene FailureReason. Se puede reintentar manualmente.
///   Cancelled : se canceló la cita asociada antes de que saliera, así
///               que ya no tiene sentido mandarlo. El dispatcher lo
///               salta y se queda como histórico.
/// </summary>
public enum WhatsAppMessageStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    Cancelled = 3,
}
