using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Commissions.Dtos;

namespace BellaSync.Application.Features.Commissions.ListPayouts;

/// <summary>
/// Historial de liquidaciones. Sin filtro de fecha → últimas 50.
/// Con from/to → cuyo período se solapa con ese rango.
/// </summary>
public sealed record ListPayoutsQuery(DateOnly? From, DateOnly? To)
    : IQuery<IReadOnlyList<CommissionPayoutResponse>>;
