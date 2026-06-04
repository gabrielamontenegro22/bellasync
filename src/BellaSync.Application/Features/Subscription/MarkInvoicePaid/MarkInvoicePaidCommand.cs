using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Subscription.Dtos;

namespace BellaSync.Application.Features.Subscription.MarkInvoicePaid;

/// <summary>
/// Marca una factura como pagada por el salón. Pensado para uso interno
/// (la admin del salón confirma que hizo la transferencia y deja la
/// referencia). El SaaSAdmin de BellaSync valida después.
///
/// Una vez marcada Paid, la suscripción se renueva +1 mes (Renew()).
/// Si estaba en Trial transiciona a Active.
/// </summary>
public sealed record MarkInvoicePaidCommand(
    Guid InvoiceId,
    string PaymentMethod,
    string? Reference)
    : ICommand<SubscriptionResponse>;
