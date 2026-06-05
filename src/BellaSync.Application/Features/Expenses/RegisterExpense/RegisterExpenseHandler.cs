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

        // 2.5. Cap por rol — el valor se configura por salón (Tenant).
        // Lectura mínima: solo el campo del cap, no traemos el tenant
        // completo. La admin no tiene cap NUNCA, independiente del valor.
        if (!_currentUser.IsSalonAdmin)
        {
            var cap = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == _currentTenant.TenantId)
                .Select(t => t.ReceptionExpenseCapCop)
                .FirstOrDefaultAsync(ct);

            // cap = null  → sin límite (admin lo configuró así).
            // cap = 0     → recepción no puede registrar egresos.
            // cap > 0     → tope; sobre este monto requiere admin.
            if (cap is decimal capValue)
            {
                if (capValue == 0m)
                {
                    return ApplicationError.Forbidden(
                        "expense.reception_disabled",
                        "La administradora del salón configuró que solo ella puede registrar egresos.");
                }
                if (amount.Amount > capValue)
                {
                    return ApplicationError.Forbidden(
                        "expense.over_reception_cap",
                        $"Egresos sobre ${capValue:N0} COP requieren autorización de la administradora del salón.");
                }
            }
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
