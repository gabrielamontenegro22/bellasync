using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Dashboard.Dtos;

namespace BellaSync.Application.Features.Dashboard.GetDashboardSummary;

/// <summary>
/// Snapshot agregado del estado del salón "ahora". Usado por:
///   - Dashboard de bienvenida (home tras login)
///   - Badges del sidebar (pendingVouchersCount)
/// </summary>
public sealed record GetDashboardSummaryQuery() : IQuery<DashboardSummaryResponse>;
