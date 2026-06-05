using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Admin.SeedDemo;
using BellaSync.Application.Features.Tenants.Dtos;
using BellaSync.Application.Features.Tenants.GetCommissionsSetting;
using BellaSync.Application.Features.Tenants.GetPaymentPolicy;
using BellaSync.Application.Features.Tenants.GetSalonHours;
using BellaSync.Application.Features.Tenants.GetTenantInfo;
using BellaSync.Application.Features.Tenants.UpdateCommissionsSetting;
using BellaSync.Application.Features.Tenants.UpdatePaymentPolicy;
using BellaSync.Application.Features.Tenants.GetReceptionPermissions;
using BellaSync.Application.Features.Tenants.UpdateReceptionPermissions;
using BellaSync.Application.Features.Tenants.UpdateSalonHours;
using BellaSync.Application.Features.Tenants.UpdateTenantInfo;
using BellaSync.Application.Features.Tenants.UploadLogo;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Acciones administrativas del salón: política de pagos, info pública,
/// horario, permisos de recepción, comisiones (toggle), logo, seed demo.
///
/// La clase está abierta a SalonAdmin + Receptionist porque varios de
/// estos endpoints son configurables (recepción puede editarlos si la
/// admin le activó el permiso correspondiente en /configuracion/permisos).
/// Cada endpoint declara su propio gate:
///   - Lectura: abierta a ambos roles (la UI necesita poder mostrar).
///   - Escritura admin-only (seed, toggle comisiones, editar permisos):
///     [Authorize(Roles = "SalonAdmin")] explícito.
///   - Escritura configurable (payment policy, salon hours, tenant info,
///     logo): [RequireReceptionPermission(Perm.CanXxx)] — admin pasa
///     siempre, recepción pasa solo con el toggle activado.
///
/// IMPORTANTE: el class-level [Authorize] se combina con AND con los
/// method-level. Si la clase exigiera "SalonAdmin", recepción nunca
/// pasaría aunque el método dijera "SalonAdmin,Receptionist". Por eso
/// la clase está abierta a ambos roles y los admin-only son explícitos
/// en cada método.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class AdminController : ControllerBase
{
    /// <summary>
    /// POST /api/Admin/seed-demo-data?date=YYYY-MM-DD
    /// Crea estilistas, servicios, clientes y citas demo para la fecha dada.
    /// Idempotente: si el dato ya existe (por nombre/teléfono/slot), lo salta.
    /// </summary>
    [HttpPost("seed-demo-data")]
    // Admin-only explícito: poblar con datos demo en producción es
    // destructivo en intención (no en datos, pero ensucia el catálogo
    // del salón). Recepción no debería poder dispararlo.
    [Authorize(Roles = "SalonAdmin")]
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
    // Lectura abierta (clase) — recepción puede ver la configuración
    // actual aunque no la pueda editar. Necesario para que el form en
    // /configuracion/pagos cargue valores aunque el SaveBar quede oculto.
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
    [RequireReceptionPermission(Perm.CanEditPaymentPolicy)]
    [ProducesResponseType(typeof(TenantPaymentPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePaymentPolicy(
        [FromBody] UpdatePaymentPolicyCommand command,
        [FromServices] ICommandHandler<UpdatePaymentPolicyCommand, TenantPaymentPolicyResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Permisos de recepción
    // ============================================================

    /// <summary>
    /// GET /api/Admin/reception-permissions
    /// Lee los permisos configurables que la admin asigna a recepción
    /// (cap de egresos, cancelar con plata, cerrar caja).
    /// </summary>
    [HttpGet("reception-permissions")]
    // Lectura abierta (clase) — recepción necesita leer SUS permisos
    // para que el hook usePermissions del frontend sepa qué mostrarle.
    // EDITAR sigue siendo admin-only (PUT más abajo).
    [ProducesResponseType(typeof(ReceptionPermissionsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionPermissions(
        [FromServices] IQueryHandler<GetReceptionPermissionsQuery, ReceptionPermissionsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetReceptionPermissionsQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// PUT /api/Admin/reception-permissions
    /// Actualiza los permisos. El dominio valida que el cap sea null
    /// (sin límite) o no-negativo.
    /// </summary>
    [HttpPut("reception-permissions")]
    // Admin-only explícito: si recepción pudiera editar sus propios
    // permisos, podría darse acceso a sí misma a TODO. Esta es la
    // raíz de la confianza del sistema configurable — la admin es la
    // única autoridad para conceder/quitar permisos.
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(ReceptionPermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateReceptionPermissions(
        [FromBody] UpdateReceptionPermissionsCommand command,
        [FromServices] ICommandHandler<UpdateReceptionPermissionsCommand, ReceptionPermissionsResponse> handler,
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
    // Lectura abierta (clase): el sidebar usa useCommissionsSetting()
    // para decidir si mostrar el item /comisiones. Recepción necesita
    // poder leerlo aunque no pueda editarlo.
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
    // Admin-only: activar/desactivar el módulo de comisiones afecta cómo
    // se pagan los estilistas (cuánto sale del salón cada mes). Decisión
    // de negocio que recepción no debería tomar.
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(CommissionsSettingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateCommissionsSetting(
        [FromBody] UpdateCommissionsSettingCommand command,
        [FromServices] ICommandHandler<UpdateCommissionsSettingCommand, CommissionsSettingResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Información general del salón
    // ============================================================

    /// <summary>
    /// GET /api/Admin/tenant-info
    /// Lee la info pública/contacto del salón (nombre, dirección, etc.).
    /// </summary>
    [HttpGet("tenant-info")]
    // Lectura abierta (clase) — el form de /configuracion/general debe
    // poder mostrar los datos actuales aun cuando recepción no pueda editar.
    [ProducesResponseType(typeof(TenantInfoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantInfo(
        [FromServices] IQueryHandler<GetTenantInfoQuery, TenantInfoResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetTenantInfoQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// PUT /api/Admin/tenant-info
    /// Actualiza la info. Slug NO se cambia por acá. Validaciones de
    /// dominio: email con @, URL http(s), maxlen por campo.
    /// </summary>
    [HttpPut("tenant-info")]
    [RequireReceptionPermission(Perm.CanEditSalonInfo)]
    [ProducesResponseType(typeof(TenantInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateTenantInfo(
        [FromBody] UpdateTenantInfoCommand command,
        [FromServices] ICommandHandler<UpdateTenantInfoCommand, TenantInfoResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Horario del salón
    // ============================================================

    /// <summary>
    /// GET /api/Admin/salon-hours
    /// Devuelve el horario completo (días, almuerzo, festivos, cierres).
    /// </summary>
    [HttpGet("salon-hours")]
    // Lectura abierta (clase) — useSalonHours hook del frontend lo usa
    // para pintar la grilla del agenda con franjas cerradas. Recepción
    // SIEMPRE necesita poder leerlo (independiente de CanEditSchedule).
    [ProducesResponseType(typeof(SalonHoursResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalonHours(
        [FromServices] IQueryHandler<GetSalonHoursQuery, SalonHoursResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetSalonHoursQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// PUT /api/Admin/salon-hours
    /// Replace-all del horario: actualiza flags, días y cierres puntuales
    /// en una transacción.
    /// </summary>
    [HttpPut("salon-hours")]
    [RequireReceptionPermission(Perm.CanEditSchedule)]
    [ProducesResponseType(typeof(SalonHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSalonHours(
        [FromBody] UpdateSalonHoursCommand command,
        [FromServices] ICommandHandler<UpdateSalonHoursCommand, SalonHoursResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    // ============================================================
    // Logo del salón (upload de archivo)
    // ============================================================

    /// <summary>
    /// POST /api/Admin/tenant/logo
    /// Multipart: campo "file" con la imagen. Reemplaza el logo actual.
    /// El archivo viejo se borra del storage si era nuestro.
    /// Máx 5MB, formatos JPG/PNG/WebP/HEIC.
    /// </summary>
    [HttpPost("tenant/logo")]
    // Logo es parte de "info pública del salón" (Tenant.LogoUrl). Si la
    // admin le activó CanEditSalonInfo a recepción, debe poder cambiar
    // el logo también — sino el form de /configuracion/general queda
    // mocho (puede editar nombre/dirección pero no logo).
    [RequireReceptionPermission(Perm.CanEditSalonInfo)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadRules.MaxFileSizeBytes + 1024)]
    [ProducesResponseType(typeof(LogoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadLogo(
        [FromForm] LogoUploadRequest form,
        [FromServices] IFileStorage storage,
        [FromServices] ICommandHandler<UploadLogoCommand> handler,
        CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { error = "El archivo es obligatorio." });
        if (form.File.Length > FileUploadRules.MaxFileSizeBytes)
            return BadRequest(new { error = "El archivo supera 5 MB." });
        if (!FileUploadRules.IsAllowedImage(form.File.ContentType))
            return BadRequest(new { error = "Solo aceptamos imágenes (JPG/PNG/WebP/HEIC)." });

        string newUrl;
        await using (var stream = form.File.OpenReadStream())
        {
            newUrl = await storage.SaveAsync(
                category: "logos",
                fileName: form.File.FileName,
                contentType: form.File.ContentType,
                content: stream,
                ct: ct);
        }

        var result = await handler.HandleAsync(new UploadLogoCommand(newUrl), ct);
        if (result.IsFailure)
        {
            // Rollback del archivo huérfano si el handler falló.
            await storage.DeleteAsync(newUrl, ct);
            return result.ToActionResult();
        }

        return Ok(new LogoUploadResponse(newUrl));
    }
}

public sealed class LogoUploadRequest
{
    public IFormFile? File { get; set; }
}

public sealed record LogoUploadResponse(string LogoUrl);
