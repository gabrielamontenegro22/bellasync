using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.Dtos;

namespace BellaSync.Application.Features.Cash.ListCashClosings;

/// <summary>
/// Historial de cierres del salón. Por default: últimos 30 días.
/// </summary>
public sealed record ListCashClosingsQuery(DateOnly? From, DateOnly? To)
    : IQuery<IReadOnlyList<CashClosingResponse>>;
