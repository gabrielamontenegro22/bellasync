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
        Provider = e.Provider,
        RegisteredByUserId = e.RegisteredByUserId,
        RegisteredByUserName = e.RegisteredByUser?.FullName,
        RegisteredAt = e.RegisteredAt,
    };
}
