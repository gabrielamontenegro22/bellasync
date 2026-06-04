using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Expenses.Dtos;

namespace BellaSync.Application.Features.Expenses.GetDailyExpenses;

/// <summary>
/// Devuelve los egresos de un día. Date opcional → default = hoy (zona Colombia).
/// </summary>
public sealed record GetDailyExpensesQuery(DateOnly? Date)
    : IQuery<IReadOnlyList<ExpenseResponse>>;
