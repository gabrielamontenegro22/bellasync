using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Subscription;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.HostedServices;

/// <summary>
/// Background service que mantiene el ciclo mensual de las suscripciones
/// SaaS al día. Corre cada hora (no más, la facturación es mensual y no
/// necesita reactividad sub-minuto):
///
///   1. Trials que vencieron sin pago → MarkPastDue + emite factura
///      Pending para que el admin pueda regularizar.
///
///   2. Subscriptions Active con CurrentPeriodEnd dentro de los próximos
///      7 días y sin factura Pending → emite factura del próximo período
///      (notificación temprana).
///
///   3. Subscriptions Active vencidas (CurrentPeriodEnd < now) sin
///      factura paga del período → MarkPastDue + emite si no hay Pending.
///
/// Cross-tenant: usa IgnoreQueryFilters porque corre fuera del scope de
/// request HTTP (no tiene TenantId).
/// </summary>
public sealed class SubscriptionDispatcherService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan EarlyIssueWindow = TimeSpan.FromDays(7);

    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionDispatcherService> _logger;

    public SubscriptionDispatcherService(
        IServiceProvider services,
        ILogger<SubscriptionDispatcherService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SubscriptionDispatcherService arrancando — corre cada {Interval}",
            Interval);

        // Pequeño delay inicial para no competir con el bootstrap.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falla en SubscriptionDispatcherService — reintenta en {Interval}",
                    Interval);
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }

        _logger.LogInformation("SubscriptionDispatcherService deteniéndose.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        // Levantamos TODAS las subs no canceladas — siempre serán pocas
        // (1 por tenant) así que la query es chica aun con miles de salones.
        var subs = await db.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status != SubscriptionStatus.Cancelled)
            .ToListAsync(ct);

        if (subs.Count == 0) return;

        var tenantIds = subs.Select(s => s.TenantId).ToList();

        // Pre-cargamos las facturas Pending para chequear duplicados sin
        // hacer N queries.
        var pendingByTenant = await db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .Where(i => tenantIds.Contains(i.TenantId)
                     && i.Status == SubscriptionInvoiceStatus.Pending)
            .GroupBy(i => i.TenantId)
            .Select(g => g.Key)
            .ToListAsync(ct);

        var hasPending = pendingByTenant.ToHashSet();

        var transitions = 0;
        var issued = 0;

        foreach (var sub in subs)
        {
            // 1. Trial vencido → PastDue + emite factura
            if (sub.Status == SubscriptionStatus.Trial
                && sub.TrialEndsAt.HasValue
                && sub.TrialEndsAt.Value < now)
            {
                sub.MarkPastDue(now);
                transitions++;
                if (!hasPending.Contains(sub.TenantId))
                {
                    if (TryIssue(db, sub, now, periodStartsFromNow: true))
                    {
                        hasPending.Add(sub.TenantId);
                        issued++;
                    }
                }
                continue;
            }

            // 2. Active vencida sin pago → PastDue + emite factura
            if (sub.Status == SubscriptionStatus.Active
                && sub.CurrentPeriodEnd < now)
            {
                sub.MarkPastDue(now);
                transitions++;
                if (!hasPending.Contains(sub.TenantId))
                {
                    if (TryIssue(db, sub, now, periodStartsFromNow: false))
                    {
                        hasPending.Add(sub.TenantId);
                        issued++;
                    }
                }
                continue;
            }

            // 3. Active con período próximo a vencer (7d) sin Pending →
            //    emisión temprana para que admin pueda pagar antes
            if (sub.Status == SubscriptionStatus.Active
                && sub.CurrentPeriodEnd <= now + EarlyIssueWindow
                && !hasPending.Contains(sub.TenantId))
            {
                if (TryIssue(db, sub, now, periodStartsFromNow: false))
                {
                    hasPending.Add(sub.TenantId);
                    issued++;
                }
            }
        }

        if (transitions > 0 || issued > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "SubscriptionDispatcher: {Transitions} transiciones + {Issued} facturas emitidas.",
                transitions, issued);
        }
    }

    /// <summary>
    /// Intenta emitir una factura para el período en curso (o el próximo
    /// si periodStartsFromNow). Devuelve false si el plan no existe en el
    /// catálogo (caso raro: alguien dropeó un plan que estaba en uso).
    /// </summary>
    private bool TryIssue(
        IApplicationDbContext db, TenantSubscription sub, DateTime now, bool periodStartsFromNow)
    {
        var plan = SubscriptionPlanCatalog.Get(sub.PlanCode);
        if (plan is null)
        {
            _logger.LogWarning(
                "Tenant {TenantId} tiene plan desconocido {Plan} — no emito factura.",
                sub.TenantId, sub.PlanCode);
            return false;
        }

        var (start, end) = periodStartsFromNow
            ? (now, now.AddMonths(1))
            : (sub.CurrentPeriodEnd, sub.CurrentPeriodEnd.AddMonths(1));

        var invoice = SubscriptionInvoice.Issue(
            tenantId: sub.TenantId,
            planCode: plan.Code,
            amount: Money.Create(plan.MonthlyPrice),
            periodStart: start,
            periodEnd: end,
            dueDate: now.AddDays(7),
            utcNow: now);

        db.SubscriptionInvoices.Add(invoice);
        return true;
    }
}
