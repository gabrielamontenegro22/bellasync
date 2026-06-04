using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Reports.Dtos;
using BellaSync.Application.Features.Reports.GetReportsSummary;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Reportes/KPIs del salón. Endpoint único "summary" que devuelve todo
/// lo que el dashboard de /reportes necesita para una sola petición.
/// Solo SalonAdmin — la recepción no necesita ver agregados de negocio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin")]
public class ReportsController : ControllerBase
{
    /// <summary>
    /// GET /api/Reports/summary?from=YYYY-MM-DD&to=YYYY-MM-DD
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Summary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromServices] IQueryHandler<GetReportsSummaryQuery, ReportsSummaryResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetReportsSummaryQuery(from, to), ct);
        return result.ToActionResult();
    }
}
