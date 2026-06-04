using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Cash.Shared;

internal static class CashClosingMapper
{
    public static CashClosingResponse ToResponse(CashClosing cc) => new()
    {
        Id = cc.Id,
        ClosedDate = cc.ClosedDate.ToString("yyyy-MM-dd"),
        BaseAmount = cc.BaseAmount.Amount,
        CashSales = cc.CashSales.Amount,
        CashExpenses = cc.CashExpenses.Amount,
        ExpectedCash = cc.ExpectedCash.Amount,
        CountedCash = cc.CountedCash.Amount,
        Diff = cc.Diff,
        DiffNote = cc.DiffNote,
        TotalAmount = cc.TotalAmount.Amount,
        ClosedAt = cc.ClosedAt,
        ClosedByUserId = cc.ClosedByUserId,
    };
}
