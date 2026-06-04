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

        if (invoice.Status == SubscriptionInvoiceStatus.Paid)
            return Result.Success();  // idempotente

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
            invoice.Validate(_currentUser.UserId.Value, now);

            // Trial → Active. Active/PastDue → Renew (extiende +1 mes).
            if (sub.Status == SubscriptionStatus.Trial)
                sub.Activate(now);
            else
                sub.Renew(now);
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
