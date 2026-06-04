using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Reports.Dtos;

namespace BellaSync.Application.Features.Reports.GetReportsSummary;

/// <summary>
/// Pide el snapshot agregado para el rango [From, To] inclusive.
/// Si From > To, el handler devuelve un error de validación.
/// Rango máximo permitido: 1 año (para evitar queries gigantes).
/// </summary>
public sealed record GetReportsSummaryQuery(
    DateOnly From,
    DateOnly To
) : IQuery<ReportsSummaryResponse>;
