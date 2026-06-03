using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Payments.Dtos;

namespace BellaSync.Application.Features.Payments.GetCustomerPayments;

/// <summary>
/// Historial de pagos del cliente — usado por el tab Pagos del CRM.
/// Devuelve pagos ordenados desc por RegisteredAt.
/// </summary>
public sealed record GetCustomerPaymentsQuery(Guid CustomerId)
    : IQuery<IReadOnlyList<PaymentResponse>>;
