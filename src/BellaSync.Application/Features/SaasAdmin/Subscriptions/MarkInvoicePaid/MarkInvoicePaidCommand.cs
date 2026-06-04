using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.MarkInvoicePaid;

/// <summary>
/// El SuperAdmin marca una factura como pagada SIN pasar por el flujo
/// Reported (el salón pagó offline, vía canal alterno, o el SuperAdmin
/// está registrando un pago histórico que no entró por la UI normal).
///
/// Diferencia con ValidatePayment: ValidatePayment requiere que la
/// factura esté en estado Reported (el salón ya la reportó). MarkPaid
/// salta ese paso y marca directamente, ideal para casos backend-only.
/// </summary>
public sealed record MarkInvoicePaidCommand(
    Guid InvoiceId,
    string PaymentMethod,
    string? Reference) : ICommand;
