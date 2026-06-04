using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.RejectPayment;

/// <summary>
/// El SuperAdmin rechaza un pago reportado (no encontró la transferencia
/// en el extracto bancario). La factura vuelve a Pending para que el
/// salón pueda re-reportar con datos corregidos.
/// </summary>
public sealed record RejectPaymentCommand(Guid InvoiceId, string Reason) : ICommand;
