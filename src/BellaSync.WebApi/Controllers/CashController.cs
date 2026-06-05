using System.Security.Claims;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.CreateCashClosing;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Cash.GetCashClosingForDate;
using BellaSync.Application.Features.Cash.GetDailyCashSummary;
using BellaSync.Application.Features.Cash.ListCashClosings;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Cierre de caja del salón. Por ahora solo expone el resumen diario,
/// pero acá pueden vivir reportes semanales/mensuales si se necesitan
/// más adelante. SalonAdmin + Receptionist pueden consultar (la
/// recepción cierra al final del día; la admin revisa el resumen).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class CashController : ControllerBase
{
    /// <summary>
    /// GET /api/Cash/daily-summary?date=YYYY-MM-DD
    /// Devuelve el resumen de caja del día indicado (zona Colombia).
    /// Si date no viene, usa "hoy".
    /// </summary>
    [HttpGet("daily-summary")]
    [ProducesResponseType(typeof(DailyCashSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDailySummary(
        [FromServices] IQueryHandler<GetDailyCashSummaryQuery, DailyCashSummaryResponse> handler,
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

        var result = await handler.HandleAsync(new GetDailyCashSummaryQuery(targetDate), ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Cierres persistidos
    // ============================================================

    /// <summary>
    /// POST /api/Cash/closings
    /// Firma el cierre del día. Una sola vez por (tenant, fecha) — si ya
    /// hay cierre devuelve 409 Conflict. Snapshea ventas/egresos en
    /// efectivo desde Payments/Expenses, así que el cierre queda inmutable
    /// aunque después se agreguen/borren movimientos del día.
    ///
    /// Por default solo admin firma. La admin puede delegar a recepción
    /// activando Tenant.ReceptionCanCloseCash desde /configuracion/permisos.
    /// El handler chequea el setting y devuelve 403 si recepción intenta
    /// sin permiso — no usamos [Authorize(Roles=...)] porque la regla es
    /// condicional al tenant, no tajante.
    /// </summary>
    [HttpPost("closings")]
    [ProducesResponseType(typeof(CashClosingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateClosing(
        [FromBody] CreateCashClosingRequest request,
        [FromServices] ICommandHandler<CreateCashClosingCommand, CashClosingResponse> handler,
        CancellationToken ct)
    {
        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var parsed)) userId = parsed;

        var command = new CreateCashClosingCommand(
            ClosedDate: request.ClosedDate,
            BaseAmount: request.BaseAmount,
            CountedCash: request.CountedCash,
            DiffNote: request.DiffNote,
            ClosedByUserId: userId);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Cash/closings?from=YYYY-MM-DD&amp;to=YYYY-MM-DD
    /// Historial de cierres. Default: últimos 30 días.
    /// </summary>
    [HttpGet("closings")]
    [ProducesResponseType(typeof(IReadOnlyList<CashClosingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListClosings(
        [FromServices] IQueryHandler<ListCashClosingsQuery, IReadOnlyList<CashClosingResponse>> handler,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        DateOnly? fromDate = null, toDate = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var f))
                return BadRequest(new { error = "from inválido (YYYY-MM-DD)." });
            fromDate = f;
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateOnly.TryParseExact(to, "yyyy-MM-dd", out var t))
                return BadRequest(new { error = "to inválido (YYYY-MM-DD)." });
            toDate = t;
        }

        var result = await handler.HandleAsync(new ListCashClosingsQuery(fromDate, toDate), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Cash/closings/by-date/{date}
    /// Devuelve el cierre de una fecha si existe, 404 si no. El frontend
    /// lo usa al cargar /caja para mostrar el pill "Caja cerrada" cuando
    /// aplica y deshabilitar el botón de cerrar.
    /// </summary>
    [HttpGet("closings/by-date/{date}")]
    [ProducesResponseType(typeof(CashClosingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClosingByDate(
        string date,
        [FromServices] IQueryHandler<GetCashClosingForDateQuery, CashClosingResponse?> handler,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
            return BadRequest(new { error = "Fecha inválida (YYYY-MM-DD)." });

        var result = await handler.HandleAsync(new GetCashClosingForDateQuery(d), ct);
        if (!result.IsSuccess) return result.ToActionResult();
        if (result.Value is null) return NotFound();
        return Ok(result.Value);
    }
}

public class CreateCashClosingRequest
{
    /// <summary>YYYY-MM-DD. Si se omite, asume hoy (Colombia).</summary>
    public string? ClosedDate { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal CountedCash { get; set; }
    public string? DiffNote { get; set; }
}
