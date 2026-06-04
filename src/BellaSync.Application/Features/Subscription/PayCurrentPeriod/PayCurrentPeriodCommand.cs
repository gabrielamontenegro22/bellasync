using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.PayCurrentPeriod;

/// <summary>
/// Endpoint inteligente para pagar la suscripción "ahora", sin que el
/// frontend tenga que saber si hay una factura pendiente o si tiene que
/// emitir una primero. El handler:
///
///   - Si hay una factura Pending → la marca paga.
///   - Si no hay Pending → emite una para el período actual y la marca
///     paga, atómicamente.
///
/// Resuelve el problema clásico "trial sin factura" — el admin clickea
/// "Activar plan ahora" y todo pasa en una sola transacción.
/// </summary>
public sealed record PayCurrentPeriodCommand(
    string PaymentMethod,
    string? Reference)
    : ICommand<SubscriptionResponse>;
