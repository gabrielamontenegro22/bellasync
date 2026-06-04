using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.Dtos;

namespace BellaSync.Application.Features.Cash.CreateCashClosing;

/// <summary>
/// Firma el cierre del día. El handler snapshea los montos del día
/// (ventas en efectivo, egresos en efectivo, total) leyéndolos de
/// Payments/Expenses para que el cierre quede inmutable.
///
/// La admin solo aporta CountedCash (lo que contó físicamente),
/// BaseAmount (cuánto puso de base esa mañana) y DiffNote
/// (explicación si no cuadra). El backend calcula Diff.
/// </summary>
public sealed record CreateCashClosingCommand(
    /// <summary>YYYY-MM-DD del día a cerrar. Default: hoy (Colombia).</summary>
    string? ClosedDate,
    decimal BaseAmount,
    decimal CountedCash,
    string? DiffNote,
    Guid? ClosedByUserId
) : ICommand<CashClosingResponse>;
