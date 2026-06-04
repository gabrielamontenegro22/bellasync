using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Subscription.IssueInvoice;

/// <summary>
/// Emite una factura Pending para el período de facturación actual del
/// salón. Idempotente: si ya existe una Pending, devuelve esa.
///
/// Usado por:
///   - SubscriptionDispatcherService (auto-emisión periódica)
///   - Admin que quiere "ver" la factura antes de pagar
///   - PayCurrentPeriod cuando no hay factura pendiente
/// </summary>
public sealed record IssueInvoiceCommand() : ICommand<Guid>;
