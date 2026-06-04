using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Application.Features.Expenses.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Expenses.RegisterExpense;

public sealed class RegisterExpenseHandler
    : ICommandHandler<RegisterExpenseCommand, ExpenseResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<RegisterExpenseHandler> _logger;

    public RegisterExpenseHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<RegisterExpenseHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
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

        Expense expense;
        try
        {
            expense = Expense.Create(
                tenantId: _currentTenant.TenantId,
                concept: command.Concept,
                amount: amount,
                method: command.Method,
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

        return Result<ExpenseResponse>.Success(ExpenseMapper.ToResponse(expense));
    }
}
