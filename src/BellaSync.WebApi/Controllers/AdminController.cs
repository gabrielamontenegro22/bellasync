using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Admin.SeedDemo;
using BellaSync.Application.Features.Tenants.Dtos;
using BellaSync.Application.Features.Tenants.GetCommissionsSetting;
using BellaSync.Application.Features.Tenants.GetPaymentPolicy;
using BellaSync.Application.Features.Tenants.UpdateCommissionsSetting;
using BellaSync.Application.Features.Tenants.UpdatePaymentPolicy;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Acciones administrativas del salón. Por ahora solo expone el seed de
/// datos demo, pero acá viven futuras acciones tipo "exportar respaldo",
/// "borrar todo" (con confirmación), etc.
///
/// Restringido a SalonAdmin — las recepcionistas no deberían poder poblar
/// con datos demo en producción.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin")]
public class AdminController : ControllerBase
{
    /// <summary>
    /// POST /api/Admin/seed-demo-data?date=YYYY-MM-DD
    /// Crea estilistas, servicios, clientes y citas demo para la fecha dada.
    /// Idempotente: si el dato ya existe (por nombre/teléfono/slot), lo salta.
    /// </summary>
    [HttpPost("seed-demo-data")]
    [ProducesResponseType(typeof(SeedDemoDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SeedDemoData(
        [FromServices] ICommandHandler<SeedDemoDataCommand, SeedDemoDataResponse> handler,
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

        var result = await handler.HandleAsync(new SeedDemoDataCommand(targetDate), ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Política de pagos del salón
    // ============================================================

    /// <summary>
    /// GET /api/Admin/payment-policy
    /// Lee los tiempos de hold y anticipación del salón actual.
    /// </summary>
    [HttpGet("payment-policy")]
    [ProducesResponseType(typeof(TenantPaymentPolicyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentPolicy(
        [FromServices] IQueryHandler<GetPaymentPolicyQuery, TenantPaymentPolicyResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetPaymentPolicyQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// PUT /api/Admin/payment-policy
    /// Actualiza los tiempos. El dominio valida que los rangos sean razonables
    /// (hold entre 1-48h, etc.) y devuelve 400 si no.
    /// </summary>
    [HttpPut("payment-policy")]
    [ProducesResponseType(typeof(TenantPaymentPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePaymentPolicy(
        [FromBody] UpdatePaymentPolicyCommand command,
        [FromServices] ICommandHandler<UpdatePaymentPolicyCommand, TenantPaymentPolicyResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Comisiones (opt-in)
    // ============================================================

    /// <summary>
    /// GET /api/Admin/commissions-setting
    /// Lee si el módulo de comisiones está activo para este salón.
    /// </summary>
    [HttpGet("commissions-setting")]
    [ProducesResponseType(typeof(CommissionsSettingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommissionsSetting(
        [FromServices] IQueryHandler<GetCommissionsSettingQuery, CommissionsSettingResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetCommissionsSettingQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// PUT /api/Admin/commissions-setting
    /// Activa/desactiva el módulo. Idempotente.
    /// </summary>
    [HttpPut("commissions-setting")]
    [ProducesResponseType(typeof(CommissionsSettingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCommissionsSetting(
        [FromBody] UpdateCommissionsSettingCommand command,
        [FromServices] ICommandHandler<UpdateCommissionsSettingCommand, CommissionsSettingResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }
}
