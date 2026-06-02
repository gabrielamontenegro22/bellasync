using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.ReleaseExpiredHolds;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Endpoints de sistema invocados por jobs externos (cron, Windows Task
/// Scheduler, healthchecks). Autenticados por header "X-Internal-Token"
/// que matchea Internal:CronToken en appsettings.
///
/// Por qué un token simple y no OAuth: estos endpoints son llamados por
/// procesos del propio servidor, no por usuarios. Un secret estable
/// alcanza. En producción, rotar el token periódicamente.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InternalController : ControllerBase
{
    private readonly InternalSettings _settings;

    public InternalController(IOptions<InternalSettings> settings)
    {
        _settings = settings.Value;
    }

    public const string TokenHeaderName = "X-Internal-Token";

    [HttpPost("release-expired-holds")]
    [ProducesResponseType(typeof(ReleaseExpiredHoldsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReleaseExpiredHolds(
        [FromServices] ICommandHandler<ReleaseExpiredHoldsCommand, ReleaseExpiredHoldsResponse> handler,
        CancellationToken ct)
    {
        if (!IsAuthorized())
            return Unauthorized();

        var result = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Lee el header directo de Request.Headers — más robusto que [FromHeader]
    /// para nombres con guiones en algunas versiones de ASP.NET.
    /// </summary>
    private bool IsAuthorized()
    {
        if (string.IsNullOrWhiteSpace(_settings.CronToken)) return false;
        if (!Request.Headers.TryGetValue(TokenHeaderName, out var values)) return false;
        var token = values.ToString();
        return string.Equals(_settings.CronToken, token, StringComparison.Ordinal);
    }

}

/// <summary>Sección "Internal" en appsettings.json.</summary>
public class InternalSettings
{
    public const string SectionName = "Internal";

    /// <summary>
    /// Token compartido entre el server y los scripts/cron que invocan
    /// endpoints internos. Debe ser largo y aleatorio en producción.
    /// </summary>
    public string CronToken { get; set; } = string.Empty;
}
