using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.CancelSubscription;

/// <summary>
/// La admin del salón cancela su suscripción a BellaSync. Acceso a
/// nuevas features cesa, pero los datos del salón se conservan
/// (clientes, citas históricas, etc.) por si decide reactivar.
///
/// Si hay facturas Pending sin pagar, no las eliminamos — quedan ahí
/// como deuda histórica. Si hay una Reported esperando validación, NO
/// se puede cancelar — primero hay que esperar la decisión.
/// </summary>
public sealed record CancelSubscriptionCommand(string? Reason)
    : ICommand<SubscriptionResponse>;
