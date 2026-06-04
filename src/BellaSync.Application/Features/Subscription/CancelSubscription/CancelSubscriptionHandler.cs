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

namespace BellaSync.Application.Features.Subscription.CancelSubscription;

public sealed class CancelSubscriptionHandler
    : ICommandHandler<CancelSubscriptionCommand, SubscriptionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> _getSub;
    private readonly ILogger<CancelSubscriptionHandler> _logger;

    public CancelSubscriptionHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        IQueryHandler<GetSubscriptionQuery, SubscriptionResponse> getSub,
        ILogger<CancelSubscriptionHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _getSub = getSub;
        _logger = logger;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        CancelSubscriptionCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized("subscription.no_tenant", "Sesión inválida.");

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == _currentTenant.TenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El salón no tiene una suscripción.");

        if (sub.Status == SubscriptionStatus.Cancelled)
            return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);

        // Guard: si hay un pago Reported esperando validación, no podemos
        // cancelar — el SuperAdmin podría aprobarlo después y dejaría
        // la sub en un estado inconsistente.
        var hasReported = await _db.SubscriptionInvoices
            .AnyAsync(i => i.TenantId == _currentTenant.TenantId
                        && i.Status == SubscriptionInvoiceStatus.Reported, ct);
        if (hasReported)
            return ApplicationError.Conflict(
                "subscription.payment_in_validation",
                "No puedes cancelar mientras hay un pago en validación. " +
                "Espera la decisión de BellaSync.");

        try
        {
            sub.Cancel(command.Reason, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Conflict("subscription.cancel_failed", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} canceló suscripción. Razón: {Reason}",
            _currentTenant.TenantId, command.Reason ?? "(no especificada)");

        return await _getSub.HandleAsync(new GetSubscriptionQuery(), ct);
    }
}
