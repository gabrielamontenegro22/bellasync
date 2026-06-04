using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.SaasAdmin.Subscriptions.Dtos;

namespace BellaSync.Application.Features.SaasAdmin.Subscriptions.ListPendingValidations;

/// <summary>
/// Cola de pagos reportados pendientes de validación, cross-tenant.
/// Solo SuperAdmin puede ejecutar esta query. Ordenado por ReportedAt
/// ascendente (más viejos primero, FIFO).
/// </summary>
public sealed record ListPendingValidationsQuery() : IQuery<IReadOnlyList<PendingValidationRow>>;
