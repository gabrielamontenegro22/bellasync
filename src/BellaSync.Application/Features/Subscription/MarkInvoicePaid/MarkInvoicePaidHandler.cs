using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Application.Features.Subscription.GetSubscription;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Subscription.MarkInvoicePaid;

/// <summary>
/// Marca Paid en la factura + Activate/Renew en TenantSubscription. Todo
/// en una sola transacción (SaveChangesAsync).
/// </summary>
public sealed class MarkInvoicePaidHandler
    : ICommandHandler<MarkInvoicePaidCommand, SubscriptionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> _getSub;
    private readonly ILogger<MarkInvoicePaidHandler> _logger;

    public MarkInvoicePaidHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> getSub,
        ILogger<MarkInvoicePaidHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _getSub = getSub;
        _logger = logger;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        MarkInvoicePaidCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized(
                "subscription.no_tenant", "Sesión inválida.");

        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
            return ApplicationError.Validation(
                "subscription.method_required", "El método de pago es obligatorio.");

        var invoice = await _db.SubscriptionInvoices
            .FirstOrDefaultAsync(i => i.Id == command.InvoiceId, ct);

        if (invoice is null)
            return ApplicationError.NotFound(
                "subscription.invoice_not_found", "Factura no encontrada.");

        if (invoice.Status == SubscriptionInvoiceStatus.Paid)
            return ApplicationError.Conflict(
                "subscription.invoice_already_paid",
                "Esta factura ya está pagada.");

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == _currentTenant.TenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El salón no tiene una suscripción activa.");

        var now = _clock.UtcNow;

        try
        {
            invoice.MarkPaid(command.PaymentMethod, command.Reference, now);

            // Si la sub estaba en Trial, esto la activa. Si ya estaba
            // Active/PastDue, extiende un mes más. Cancelled lanza —
            // que es lo que queremos (no se puede pagar una sub cancelada).
            if (sub.Status == SubscriptionStatus.Trial)
                sub.Activate(now);
            else
                sub.Renew(now);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.payment_rejected", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} pagó factura {InvoiceId} con {Method}",
            _currentTenant.TenantId, invoice.Id, command.PaymentMethod);

        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
