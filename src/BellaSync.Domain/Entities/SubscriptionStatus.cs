namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del ciclo de vida de la suscripción de un salón a BellaSync
/// (lo que el salón le paga a la empresa).
///
///   Trial      — período de prueba gratuita (14 días por default).
///                Acceso completo, no se factura.
///   Active     — suscripción al día, paga puntualmente.
///   PastDue    — al menos una factura vencida sin pagar. Acceso
///                degradado pero no bloqueado todavía.
///   Cancelled  — el salón canceló. Conservamos la data por
///                trazabilidad pero no acceso a nuevas funciones.
/// </summary>
public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
}
