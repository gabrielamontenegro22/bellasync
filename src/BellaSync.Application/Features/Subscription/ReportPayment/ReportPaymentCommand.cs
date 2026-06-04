using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.ReportPayment;

/// <summary>
/// El SalonAdmin reporta que hizo la transferencia bancaria para pagar
/// su suscripción. La factura queda en estado Reported, esperando que
/// el SuperAdmin verifique contra el extracto bancario.
///
/// Smart: si no hay factura Pending, primero emite una para el período
/// actual y después la reporta — todo en una transacción.
///
/// LA SUSCRIPCIÓN NO SE ACTIVA hasta que el SuperAdmin valide. La
/// admin del salón debe esperar 1-2 días hábiles.
/// </summary>
public sealed record ReportPaymentCommand(
    string PaymentMethod,
    string? Reference)
    : ICommand<SubscriptionResponse>;
