using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.MarkInvoicePaid;

/// <summary>
/// Marca una factura como Paid directamente (sin requerir Reported).
/// Pensado para casos offline: el salón pagó por canal alterno
/// (cheque/efectivo en mano/transferencia con referencia que se perdió)
/// y el SuperAdmin lo registra manualmente.
///
/// Usa el método `MarkPaid` del dominio, que es más permisivo que
/// `Validate` (acepta Pending o Reported como origen).
/// </summary>
public sealed class MarkInvoicePaidHandler : ICommandHandler<MarkInvoicePaidCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<MarkInvoicePaidHandler> _logger;

    public MarkInvoicePaidHandler(
        IApplicationDbContext db,
        IClock clock,
        ILogger<MarkInvoicePaidHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(MarkInvoicePaidCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
            return ApplicationError.Validation(
                "saas_admin.method_required",
                "El método de pago es obligatorio.");

        var invoice = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, ct);

        if (invoice is null)
            return ApplicationError.NotFound(
                "subscription.invoice_not_found", "Factura no encontrada.");

        var sub = await _db.TenantSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == invoice.TenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El tenant no tiene una suscripción asociada.");

        var now = _clock.UtcNow;

        try
        {
            invoice.MarkPaid(command.PaymentMethod, command.Reference, now);
            if (sub.Status == SubscriptionStatus.Trial)
                sub.ActivateFromInvoice(invoice.PeriodEnd, now);
            else
                sub.RenewFromInvoice(invoice.PeriodEnd, now);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.mark_paid_failed", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SuperAdmin marcó Paid offline factura {InvoiceId} del tenant {TenantId} vía {Method}",
            invoice.Id, invoice.TenantId, command.PaymentMethod);

        return Result.Success();
    }
}
