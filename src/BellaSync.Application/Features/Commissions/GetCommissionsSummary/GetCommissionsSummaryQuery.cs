using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Commissions.Dtos;

namespace BellaSync.Application.Features.Commissions.GetCommissionsSummary;

/// <summary>
/// Resumen de comisiones para un rango de fechas (zona Colombia).
/// Devuelve una fila por estilista que tenga al menos un pago O un
/// payout en el rango.
/// </summary>
public sealed record GetCommissionsSummaryQuery(DateOnly From, DateOnly To)
    : IQuery<CommissionsSummaryResponse>;
