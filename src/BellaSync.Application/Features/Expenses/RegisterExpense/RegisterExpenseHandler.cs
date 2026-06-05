using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Application.Features.Expenses.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Expenses.RegisterExpense;

public sealed class RegisterExpenseHandler
    : ICommandHandler<RegisterExpenseCommand, ExpenseResponse>
{
    /// <summary>
    /// Tope (en COP) para egresos registrados por recepción sin pasar
    /// por admin. Pensado para gastos chicos del día (almuerzo del equipo,
    /// domicilios, insumos menores). Por encima se asume que requiere
    /// decisión de la admin (compra grande a proveedor, propinas grandes).
    /// Hardcoded por ahora; en una iteración futura podría vivir en
    /// TenantSettings para que cada salón lo ajuste.
    /// </summary>
    private const decimal ReceptionExpenseCapCop = 100_000m;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<RegisterExpenseHandler> _logger;

    public RegisterExpenseHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<RegisterExpenseHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ExpenseResponse>> HandleAsync(
        RegisterExpenseCommand command, CancellationToken ct)
    {
        // 1. Validación de superficie (otras invariantes las cubre el factory
        //    del dominio, pero queremos mensajes accionables para el UI).
        if (string.IsNullOrWhiteSpace(command.Concept))
        {
            return ApplicationError.Validation(
                "expense.concept_required",
                "El concepto del egreso es obligatorio.");
        }
        if (command.Concept.Trim().Length > 200)
        {
            return ApplicationError.Validation(
                "expense.concept_too_long",
                "El concepto no puede pasar de 200 caracteres.");
        }

        // 2. Money + factory del dominio.
        Money amount;
        try
        {
            amount = Money.Create(command.Amount);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("expense.invalid_amount", ex.Message);
        }

        // 2.5. Cap por rol: recepción tiene tope para evitar que registre
        // egresos enormes sin que la admin se entere. La admin no tiene cap.
        if (!_currentUser.IsSalonAdmin && amount.Amount > ReceptionExpenseCapCop)
        {
            return ApplicationError.Forbidden(
                "expense.over_reception_cap",
                $"Egresos sobre ${ReceptionExpenseCapCop:N0} COP requieren autorización de la administradora del salón.");
        }

        Expense expense;
        try
        {
            expense = Expense.Create(
                tenantId: _currentTenant.TenantId,
                concept: command.Concept,
                amount: amount,
                method: command.Method,
                provider: command.Provider,
                registeredByUserId: command.RegisteredByUserId,
                utcNow: _clock.UtcNow);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("expense.invalid", ex.Message);
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Egreso {ExpenseId} registrado: {Concept} {Amount} ({Method})",
            expense.Id, expense.Concept, expense.Amount.Amount, expense.Method);

        // Re-leer con Include para que el mapper devuelva el nombre del
        // user que registró (aparece en la sección Egresos de /caja).
        var created = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.RegisteredByUser)
            .FirstAsync(e => e.Id == expense.Id, ct);

        return Result<ExpenseResponse>.Success(ExpenseMapper.ToResponse(created));
    }
}
