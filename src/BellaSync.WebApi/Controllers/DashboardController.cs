using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Dashboard.Dtos;
using BellaSync.Application.Features.Dashboard.GetDashboardSummary;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoint del dashboard / home del salón. Tanto SalonAdmin como
/// Receptionist lo necesitan: la admin para su home tras login, la
/// recepción para los badges del sidebar (vouchers pendientes).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromServices] IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetDashboardSummaryQuery(), ct);
        return result.ToActionResult();
    }
}
