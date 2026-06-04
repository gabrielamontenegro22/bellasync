using System.Security.Claims;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Expenses.Dtos;
using BellaSync.Application.Features.Expenses.GetDailyExpenses;
using BellaSync.Application.Features.Expenses.RegisterExpense;
using BellaSync.Domain.Entities;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Egresos (gastos) del día del salón: compras a proveedor, domicilio,
/// propinas pagadas en efectivo, etc. SalonAdmin + Receptionist
/// pueden registrar y consultar (la recepción anota durante el día,
/// la admin revisa al cerrar).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class ExpensesController : ControllerBase
{
    /// <summary>
    /// POST /api/Expenses
    /// Registra un egreso del día del salón actual.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterExpenseRequest request,
        [FromServices] ICommandHandler<RegisterExpenseCommand, ExpenseResponse> handler,
        CancellationToken ct)
    {
        // UserId del JWT (claim sub / NameIdentifier).
        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var parsed)) userId = parsed;

        var command = new RegisterExpenseCommand(
            Concept: request.Concept,
            Amount: request.Amount,
            Method: request.Method,
            RegisteredByUserId: userId);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Expenses?date=YYYY-MM-DD
    /// Lista los egresos de un día. Sin date = hoy (zona Colombia).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<GetDailyExpensesQuery, IReadOnlyList<ExpenseResponse>> handler,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        DateOnly? targetDate = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
                return BadRequest(new { error = "Formato de fecha inválido. Usar YYYY-MM-DD." });
            targetDate = parsed;
        }

        var result = await handler.HandleAsync(new GetDailyExpensesQuery(targetDate), ct);
        return result.ToActionResult();
    }
}

/// <summary>
/// Body del POST /api/Expenses.
/// </summary>
public class RegisterExpenseRequest
{
    public string Concept { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Default Cash si no viene en el JSON (caso típico del UI).</summary>
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
}
