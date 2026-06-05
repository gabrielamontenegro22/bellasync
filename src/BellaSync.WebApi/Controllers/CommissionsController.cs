using System.Security.Claims;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Commissions.Dtos;
using BellaSync.Application.Features.Commissions.GetCommissionsSummary;
using BellaSync.Application.Features.Commissions.LiquidateCommissions;
using BellaSync.Application.Features.Commissions.ListPayouts;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Comisiones de estilistas. Info sensible (cuánto cobra cada uno).
///
/// Autorización:
///   - Admin: lectura y liquidación SIEMPRE.
///   - Recepción: solo lectura SI la admin le activó CanViewCommissions
///     en /configuracion/permisos (filter de clase). LIQUIDAR sigue
///     admin-only (override en POST /payouts) — la liquidación maneja
///     plata real, recepción nunca debería disparar transferencias.
///
/// El módulo además es opt-in a nivel tenant: la admin lo activa desde
/// /api/Admin/commissions-setting. Estos endpoints igual responden
/// aunque esté OFF — el frontend es el que esconde la pantalla.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
[RequireReceptionPermission(Perm.CanViewCommissions)]
public class CommissionsController : ControllerBase
{
    /// <summary>
    /// GET /api/Commissions/summary?from=YYYY-MM-DD&amp;to=YYYY-MM-DD
    /// Resumen del período por estilista (cobrado, comisión, ya pagado,
    /// pendiente). Sin from/to → este mes corriente Colombia.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(CommissionsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(
        [FromServices] IQueryHandler<GetCommissionsSummaryQuery, CommissionsSummaryResponse> handler,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        if (!TryParseRange(from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(new { error });

        var result = await handler.HandleAsync(new GetCommissionsSummaryQuery(fromDate, toDate), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// POST /api/Commissions/payouts
    /// Marca una liquidación realizada — admin le pagó X al estilista
    /// cubriendo el período from-to.
    /// </summary>
    [HttpPost("payouts")]
    // Override del filtro del controller: liquidar es plata real,
    // siempre admin. Recepción con CanViewCommissions solo lee.
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(CommissionPayoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePayout(
        [FromBody] CreatePayoutRequest request,
        [FromServices] ICommandHandler<LiquidateCommissionsCommand, CommissionPayoutResponse> handler,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.PeriodFrom, "yyyy-MM-dd", out var fromDate))
            return BadRequest(new { error = "PeriodFrom inválido (YYYY-MM-DD)." });
        if (!DateOnly.TryParseExact(request.PeriodTo, "yyyy-MM-dd", out var toDate))
            return BadRequest(new { error = "PeriodTo inválido (YYYY-MM-DD)." });

        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var parsed)) userId = parsed;

        var cmd = new LiquidateCommissionsCommand(
            StylistId: request.StylistId,
            Amount: request.Amount,
            PeriodFrom: fromDate,
            PeriodTo: toDate,
            Notes: request.Notes,
            PaidByUserId: userId);

        var result = await handler.HandleAsync(cmd, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Commissions/payouts?from=&amp;to=
    /// Historial de liquidaciones. Default: últimas 100.
    /// </summary>
    [HttpGet("payouts")]
    [ProducesResponseType(typeof(IReadOnlyList<CommissionPayoutResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPayouts(
        [FromServices] IQueryHandler<ListPayoutsQuery, IReadOnlyList<CommissionPayoutResponse>> handler,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        DateOnly? fromDate = null, toDate = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var f))
                return BadRequest(new { error = "from inválido." });
            fromDate = f;
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateOnly.TryParseExact(to, "yyyy-MM-dd", out var t))
                return BadRequest(new { error = "to inválido." });
            toDate = t;
        }

        var result = await handler.HandleAsync(new ListPayoutsQuery(fromDate, toDate), ct);
        return result.ToActionResult();
    }

    // ============================================================

    private static bool TryParseRange(
        string? from, string? to,
        out DateOnly fromDate, out DateOnly toDate, out string? error)
    {
        // Default sin params: este mes hasta hoy (Colombia UTC-5).
        var nowColombia = DateTime.UtcNow.AddHours(-5);
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
        {
            fromDate = new DateOnly(nowColombia.Year, nowColombia.Month, 1);
            toDate = DateOnly.FromDateTime(nowColombia);
            error = null;
            return true;
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out fromDate))
        {
            toDate = default;
            error = "from inválido (YYYY-MM-DD).";
            return false;
        }
        if (!DateOnly.TryParseExact(to, "yyyy-MM-dd", out toDate))
        {
            error = "to inválido (YYYY-MM-DD).";
            return false;
        }
        error = null;
        return true;
    }
}

public class CreatePayoutRequest
{
    public Guid StylistId { get; set; }
    public decimal Amount { get; set; }
    public string PeriodFrom { get; set; } = string.Empty;
    public string PeriodTo { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
