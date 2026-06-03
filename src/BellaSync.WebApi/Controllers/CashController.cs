using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.Dtos;
using BellaSync.Application.Features.Cash.GetDailyCashSummary;
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
}
