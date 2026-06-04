using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Expenses.RegisterExpense;

/// <summary>
/// Registra un egreso del día (compra a proveedor, domicilio, propina
/// pagada en efectivo, etc.). No descuenta de ninguna caja virtual —
/// solo deja el registro contable. El arqueo del cierre lo lee al
/// final del día para calcular el efectivo esperado.
/// </summary>
public sealed record RegisterExpenseCommand(
    string Concept,
    decimal Amount,
    PaymentMethod Method,
    /// <summary>
    /// Quién registra el egreso. El controller lo extrae del JWT.
    /// null permitido para tests / scripts internos.
    /// </summary>
    Guid? RegisteredByUserId
) : ICommand<ExpenseResponse>;
