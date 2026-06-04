namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado de una factura mensual de suscripción.
///
///   Pending — emitida, esperando pago. Tiene DueDate.
///   Paid    — la admin del salón pagó (marcada manualmente por
///             SaaSAdmin de BellaSync por ahora; futuro: webhook
///             de pasarela).
///   Failed  — el pago intentó procesarse y falló. Reintentar.
///   Waived  — perdón especial (regalo, promo, courtesy). No cuenta
///             como deuda. Marcado por SaaSAdmin.
/// </summary>
public enum SubscriptionInvoiceStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Waived = 3,
}
