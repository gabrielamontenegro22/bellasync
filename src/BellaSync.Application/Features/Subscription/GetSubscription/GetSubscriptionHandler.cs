using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Subscription.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Subscription.GetSubscription;

/// <summary>
/// Compone SubscriptionResponse para el tenant actual. Si por alguna razón
/// el tenant no tiene aún una TenantSubscription (caso edge: tenants
/// creados antes del sprint de suscripción), la auto-crea en trial con
/// el plan default — así la pantalla nunca falla con 404 para un tenant
/// legítimo.
///
/// Reglas:
///   - PlanName/MonthlyPrice se resuelven desde el catálogo estático.
///   - DaysUntilNextCharge se calcula contra UtcNow (negativo si vencido).
///   - TrialEndingSoon = true si Status=Trial y faltan ≤3 días.
///   - NextDueInvoice = la factura Pending con DueDate más próximo.
///   - Invoices: últimos 12 meses ordenados por IssuedAt desc.
///   - AvailablePlans incluye una marca IsCurrent por plan.
/// </summary>
public sealed class GetSubscriptionHandler
    : IQueryHandler<GetSubscriptionQuery, SubscriptionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;

    public GetSubscriptionHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<Result<SubscriptionResponse>> HandleAsync(
        GetSubscriptionQuery query, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized(
                "subscription.no_tenant", "Sesión inválida.");

        var tenantId = _currentTenant.TenantId;
        var now = _clock.UtcNow;

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        // Auto-bootstrap defensivo: si el tenant existía antes del sprint
        // de Suscripción y no tiene aún su TenantSubscription, la creamos
        // acá. Los tenants nuevos vienen ya con la sub creada en
        // RegisterSalonHandler — esto cubre solamente los legacy.
        if (sub is null)
        {
            sub = TenantSubscription.StartTrial(
                tenantId: tenantId,
                planCode: SubscriptionPlanCatalog.DefaultPlanCode,
                trialDays: SubscriptionPlanCatalog.DefaultTrialDays,
                utcNow: now);
            _db.TenantSubscriptions.Add(sub);
            await _db.SaveChangesAsync(ct);
        }

        var plan = SubscriptionPlanCatalog.Get(sub.PlanCode);

        var invoicesQ = await _db.SubscriptionInvoices
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.IssuedAt)
            .Take(12)
            .Select(i => new
            {
                i.Id,
                i.PlanCode,
                Amount = i.Amount.Amount,
                i.PeriodStart,
                i.PeriodEnd,
                i.DueDate,
                i.IssuedAt,
                i.Status,
                i.PaidAt,
                i.PaymentMethod,
                i.Reference,
                i.Note,
            })
            .ToListAsync(ct);

        var invoiceRows = invoicesQ
            .Select(i => new InvoiceRow
            {
                Id = i.Id,
                PlanCode = i.PlanCode,
                PlanName = SubscriptionPlanCatalog.Get(i.PlanCode)?.Name ?? i.PlanCode,
                Amount = i.Amount,
                PeriodStart = i.PeriodStart,
                PeriodEnd = i.PeriodEnd,
                DueDate = i.DueDate,
                IssuedAt = i.IssuedAt,
                Status = i.Status.ToString(),
                PaidAt = i.PaidAt,
                PaymentMethod = i.PaymentMethod,
                Reference = i.Reference,
                Note = i.Note,
            })
            .ToList();

        var nextDue = invoiceRows
            .Where(i => i.Status == nameof(SubscriptionInvoiceStatus.Pending))
            .OrderBy(i => i.DueDate)
            .FirstOrDefault();

        // Ceiling: si faltan 5d 23h, mejor mostrar "6 días" que "5".
        var daysUntilNextCharge = (int)Math.Ceiling((sub.CurrentPeriodEnd - now).TotalDays);
        var trialEndingSoon = sub.Status == SubscriptionStatus.Trial
            && sub.TrialEndsAt.HasValue
            && (sub.TrialEndsAt.Value - now).TotalDays <= 3;

        var availablePlans = SubscriptionPlanCatalog.All
            .Select(p => new PlanOption
            {
                Code = p.Code,
                Name = p.Name,
                Tagline = p.Tagline,
                MonthlyPrice = p.MonthlyPrice,
                Features = p.Features,
                IsHighlighted = p.IsHighlighted,
                IsCurrent = p.Code == sub.PlanCode,
            })
            .ToList();

        var response = new SubscriptionResponse
        {
            PlanCode = sub.PlanCode,
            PlanName = plan?.Name ?? sub.PlanCode,
            PlanTagline = plan?.Tagline ?? string.Empty,
            MonthlyPrice = plan?.MonthlyPrice ?? 0m,
            Features = plan?.Features ?? new List<string>(),
            Status = sub.Status.ToString(),
            StartedAt = sub.StartedAt,
            CurrentPeriodEnd = sub.CurrentPeriodEnd,
            TrialEndsAt = sub.TrialEndsAt,
            CancelledAt = sub.CancelledAt,
            DaysUntilNextCharge = daysUntilNextCharge,
            TrialEndingSoon = trialEndingSoon,
            AvailablePlans = availablePlans,
            Invoices = invoiceRows,
            NextDueInvoice = nextDue,
        };

        return Result<SubscriptionResponse>.Success(response);
    }
}
