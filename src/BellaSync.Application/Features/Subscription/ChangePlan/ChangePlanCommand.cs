using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.ChangePlan;

/// <summary>
/// Cambia el plan de la suscripción del salón actual. Para v1 el cambio
/// es inmediato (no prorratea): el monto del próximo cobro será el del
/// plan nuevo. Las facturas ya emitidas conservan su monto histórico.
///
/// Devuelve el SubscriptionResponse actualizado para que el frontend
/// refresque la pantalla sin un round-trip extra.
/// </summary>
public sealed record ChangePlanCommand(string PlanCode)
    : ICommand<SubscriptionResponse>;
