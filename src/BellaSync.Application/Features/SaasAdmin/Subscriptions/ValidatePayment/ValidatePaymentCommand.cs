using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.ValidatePayment;

/// <summary>
/// El SuperAdmin marca como Paid una factura Reported. Esto:
///   - Setea PaidAt + ValidatedByUserId + ValidatedAt.
///   - Activa/renueva la suscripción del tenant (Trial→Active /
///     PastDue→Active+Renew / Active→Renew).
/// </summary>
public sealed record ValidatePaymentCommand(Guid InvoiceId) : ICommand;
