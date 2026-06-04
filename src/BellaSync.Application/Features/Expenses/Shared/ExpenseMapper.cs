using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Expenses.Shared;

internal static class ExpenseMapper
{
    public static ExpenseResponse ToResponse(Expense e) => new()
    {
        Id = e.Id,
        Concept = e.Concept,
        Amount = e.Amount.Amount,
        Method = e.Method.ToString(),
        RegisteredByUserId = e.RegisteredByUserId,
        RegisteredAt = e.RegisteredAt,
    };
}
