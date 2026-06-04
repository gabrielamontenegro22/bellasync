using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.Dtos;
using BellaSync.Application.Features.Subscription;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.ListPendingValidations;

/// <summary>
/// Devuelve TODAS las facturas en estado Reported de TODOS los tenants,
/// joineadas con info del salón para que el SuperAdmin pueda decidir.
///
/// IgnoreQueryFilters porque el SuperAdmin no tiene TenantId — el
/// filtro global por defecto-cerrado bloquearía todo.
/// </summary>
public sealed class ListPendingValidationsHandler
    : IQueryHandler<ListPendingValidationsQuery, IReadOnlyList<PendingValidationRow>>
{
    private readonly IApplicationDbContext _db;

    public ListPendingValidationsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PendingValidationRow>>> HandleAsync(
        ListPendingValidationsQuery query, CancellationToken ct)
    {
        var reported = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .Where(i => i.Status == SubscriptionInvoiceStatus.Reported)
            .OrderBy(i => i.ReportedAt)
            .ToListAsync(ct);

        if (reported.Count == 0)
            return Result<IReadOnlyList<PendingValidationRow>>.Success(
                Array.Empty<PendingValidationRow>());

        var tenantIds = reported.Select(i => i.TenantId).Distinct().ToList();
        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, ct);

        var rows = reported
            .Select(i =>
            {
                var tenant = tenants.GetValueOrDefault(i.TenantId);
                var plan = SubscriptionPlanCatalog.Get(i.PlanCode);
                return new PendingValidationRow
                {
                    InvoiceId = i.Id,
                    TenantId = i.TenantId,
                    TenantName = tenant?.Name ?? "(salón desconocido)",
                    TenantSlug = tenant?.Slug ?? "",
                    PlanCode = i.PlanCode,
                    PlanName = plan?.Name ?? i.PlanCode,
                    Amount = i.Amount.Amount,
                    IssuedAt = i.IssuedAt,
                    DueDate = i.DueDate,
                    ReportedAt = i.ReportedAt ?? i.IssuedAt,
                    ReportedMethod = i.ReportedMethod ?? "—",
                    ReportedReference = i.ReportedReference,
                    PeriodStart = i.PeriodStart,
                    PeriodEnd = i.PeriodEnd,
                };
            })
            .ToList();

        return Result<IReadOnlyList<PendingValidationRow>>.Success(rows);
    }
}
