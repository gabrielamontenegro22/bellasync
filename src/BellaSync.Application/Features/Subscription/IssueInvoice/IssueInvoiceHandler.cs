using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Subscription.IssueInvoice;

/// <summary>
/// Crea (o devuelve si ya existe) la factura Pending del período actual
/// para el tenant. Período inferido del estado de la suscripción:
///
///   - Active: período = [CurrentPeriodEnd − 1mes, CurrentPeriodEnd]
///   - Trial: período = [now, now + 1mes] (factura para activar)
///   - PastDue: período = [CurrentPeriodEnd, CurrentPeriodEnd + 1mes]
///   - Cancelled: rechazado
///
/// DueDate = now + 7 días. Si ya hay una Pending, no se crea otra
/// (devuelve esa).
/// </summary>
public sealed class IssueInvoiceHandler
    : ICommandHandler<IssueInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<IssueInvoiceHandler> _logger;

    public IssueInvoiceHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<IssueInvoiceHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(
        IssueInvoiceCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized(
                "subscription.no_tenant", "Sesión inválida.");

        var tenantId = _currentTenant.TenantId;
        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (sub is null)
            return ApplicationError.NotFound(
                "subscription.not_found",
                "El salón no tiene una suscripción.");

        if (sub.Status == SubscriptionStatus.Cancelled)
            return ApplicationError.Conflict(
                "subscription.cancelled",
                "No se puede emitir factura: la suscripción está cancelada.");

        // Idempotencia: si ya hay una Pending, devuelve esa.
        var existingPending = await _db.SubscriptionInvoices
            .Where(i => i.TenantId == tenantId
                     && i.Status == SubscriptionInvoiceStatus.Pending)
            .OrderByDescending(i => i.IssuedAt)
            .FirstOrDefaultAsync(ct);

        if (existingPending is not null)
            return Result<Guid>.Success(existingPending.Id);

        var plan = SubscriptionPlanCatalog.Get(sub.PlanCode);
        if (plan is null)
            return ApplicationError.Validation(
                "subscription.plan_unknown",
                $"El plan '{sub.PlanCode}' no existe en el catálogo.");

        var now = _clock.UtcNow;
        var (periodStart, periodEnd) = ComputePeriod(sub, now);

        try
        {
            var invoice = SubscriptionInvoice.Issue(
                tenantId: tenantId,
                planCode: plan.Code,
                amount: Money.Create(plan.MonthlyPrice),
                periodStart: periodStart,
                periodEnd: periodEnd,
                dueDate: now.AddDays(7),
                utcNow: now);

            _db.SubscriptionInvoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Factura emitida tenant {TenantId} plan {Plan} ${Amount} due {Due}",
                tenantId, plan.Code, plan.MonthlyPrice, invoice.DueDate);

            return Result<Guid>.Success(invoice.Id);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("subscription.invoice_invalid", ex.Message);
        }
    }

    private static (DateTime start, DateTime end) ComputePeriod(
        TenantSubscription sub, DateTime now) => sub.Status switch
    {
        // Active: fact por el período en curso (ya empezó, termina en CurrentPeriodEnd).
        SubscriptionStatus.Active =>
            (sub.CurrentPeriodEnd.AddMonths(-1), sub.CurrentPeriodEnd),

        // Trial: factura para activar — período nuevo que arranca al pagar.
        SubscriptionStatus.Trial =>
            (now, now.AddMonths(1)),

        // PastDue: bug histórico M9 — usábamos (CurrentPeriodEnd, +1mes)
        // que si la sub estuvo abandonada por meses daba un período viejo
        // ya consumido. La admin pagaba creyendo cubrir el próximo mes
        // y en realidad cubría el mes vencido → seguía PastDue después.
        // Fix: arrancar el período NUEVO desde ahora, no desde la fecha
        // del período viejo vencido.
        SubscriptionStatus.PastDue =>
            (now, now.AddMonths(1)),

        _ => (now, now.AddMonths(1)),
    };
}
