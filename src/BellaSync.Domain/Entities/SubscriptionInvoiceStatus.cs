namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado de una factura mensual de suscripción.
///
///   Pending  — emitida, esperando que la admin del salón haga la
///              transferencia.
///   Reported — la admin del salón reportó que ya transfirió, con
///              método + referencia. Pendiente de validación por el
///              SuperAdmin (dueño de BellaSync) contra el extracto
///              bancario. La suscripción NO se activa hasta validar.
///   Paid     — el SuperAdmin verificó la transferencia en su banco
///              y la marcó válida. La suscripción se activa/renueva.
///   Failed   — un intento de procesamiento automático falló (futuro,
///              cuando se integre pasarela).
///   Waived   — cortesía/promo. No se cobra. Marcado por SuperAdmin.
/// </summary>
public enum SubscriptionInvoiceStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Waived = 3,
    Reported = 4,
}
