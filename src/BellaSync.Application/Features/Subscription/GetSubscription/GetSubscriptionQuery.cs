using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.GetSubscription;

/// <summary>
/// Trae el snapshot completo de la suscripción del salón actual:
/// plan vigente, fechas, historial de invoices, catálogo de planes y
/// la próxima factura pendiente. Una sola call para la pantalla
/// /configuracion/suscripcion.
/// </summary>
public sealed record GetSubscriptionQuery() : IQuery<SubscriptionResponse>;
