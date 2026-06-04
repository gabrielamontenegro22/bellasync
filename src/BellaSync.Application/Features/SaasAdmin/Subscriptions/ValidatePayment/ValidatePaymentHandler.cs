using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.ValidatePayment;

/// <summary>
/// Aprueba un pago reportado. Solo SuperAdmin. Como SuperAdmin no tiene
/// TenantId, usamos IgnoreQueryFilters para llegar a la factura y a su
/// suscripción en cualquier tenant.
///
/// Idempotente: si la factura ya está Paid, devuelve OK silenciosamente.
/// </summary>
public sealed class ValidatePaymentHandler : ICommandHandler<ValidatePaymentCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<ValidatePaymentHandler> _logger;

    public ValidatePaymentHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<ValidatePaymentHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ValidatePaymentCommand command, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return ApplicationError.Unauthorized(
                "saas_admin.no_user", "No se pudo identificar al SuperAdmin.");

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

        // Idempotencia con reconciliación (bug C4 del audit): si la factura
        // ya está Paid pero la sub no refleja el pago (ej. crash entre
        // invoice.Validate y sub.Renew en una corrida anterior), corremos
        // sub.Renew/Activate ahora para reconciliar. Sin esto, una sub
        // podía quedar PastDue eternamente aunque la factura figuraba Paid.
        if (invoice.Status == SubscriptionInvoiceStatus.Paid)
        {
            var subAlreadyCoversPeriod =
                sub.Status == SubscriptionStatus.Active
                && sub.CurrentPeriodEnd >= invoice.PeriodEnd;
            if (subAlreadyCoversPeriod) return Result.Success();

            // Reconciliar sin re-marcar la invoice (que ya está Paid).
            try
            {
                if (sub.Status == SubscriptionStatus.Trial)
                    sub.ActivateFromInvoice(invoice.PeriodEnd, now);
                else
                    sub.RenewFromInvoice(invoice.PeriodEnd, now);
            }
            catch (DomainException ex)
            {
                return ApplicationError.Conflict("subscription.validation_failed", ex.Message);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Reconciliación: invoice {InvoiceId} ya estaba Paid pero sub {TenantId} no reflejaba el pago. Sincronizado.",
                invoice.Id, invoice.TenantId);
            return Result.Success();
        }

        try
        {
            invoice.Validate(_currentUser.UserId.Value, now);

            // Trial → Active. Active/PastDue → Renew. Usamos la variante
            // FromInvoice para mantener el período facturado == cobrado
            // (bug M6 del audit: antes había 1 día de desfase entre lo
            // que cobrábamos y lo que activábamos).
            if (sub.Status == SubscriptionStatus.Trial)
                sub.ActivateFromInvoice(invoice.PeriodEnd, now);
            else
                sub.RenewFromInvoice(invoice.PeriodEnd, now);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.validation_failed", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SuperAdmin {UserId} validó factura {InvoiceId} del tenant {TenantId}",
            _currentUser.UserId, invoice.Id, invoice.TenantId);

        return Result.Success();
    }
}
