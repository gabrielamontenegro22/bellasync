using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.Dtos;

namespace BellaSync.Application.Features.Cash.GetDailyCashSummary;

/// <summary>
/// Resumen de caja del día indicado (zona horaria Colombia UTC-5).
/// Si Date es null, usa "hoy en Colombia".
/// </summary>
public sealed record GetDailyCashSummaryQuery(DateOnly? Date)
    : IQuery<DailyCashSummaryResponse>;
