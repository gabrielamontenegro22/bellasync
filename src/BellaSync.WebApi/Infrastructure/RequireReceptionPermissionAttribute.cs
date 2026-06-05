using BellaSync.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BellaSync.WebApi.Infrastructure;

/// <summary>
/// Atributo de autorización dinámica basado en los permisos
/// configurables del tenant (Tenant.ReceptionCanXxx).
///
/// Regla:
///   - Si el user es SalonAdmin → siempre pasa.
///   - Si es Receptionist → consulta IReceptionPermissionsService
///     y decide según el toggle que la admin configuró en
///     /configuracion/permisos.
///   - Cualquier otro rol (Stylist, anónimo) → 403.
///
/// Uso típico:
///   [HttpPost]
///   [Authorize(Roles = "SalonAdmin,Receptionist")]   // gate por rol
///   [RequireReceptionPermission(Perm.CanEditStylists)] // gate por toggle
///   public async Task&lt;IActionResult&gt; Create(...)
///
/// Por qué este patrón en vez de meter el chequeo en cada handler:
///  - Una sola línea por endpoint, no toca lógica de dominio.
///  - El endpoint se autodocumenta: "este endpoint requiere CanEditStylists".
///  - Cambiar el chequeo (mensajes, telemetría) es un solo lugar.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireReceptionPermissionAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Nombre del flag a chequear (sin el prefijo "Reception").</summary>
    public string Permission { get; }

    public RequireReceptionPermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var currentUser = services.GetRequiredService<ICurrentUserService>();

        // Admin pasa siempre.
        if (currentUser.IsSalonAdmin)
        {
            await next();
            return;
        }

        // Recepción (y otros) chequean el toggle del tenant.
        var permsService = services.GetRequiredService<IReceptionPermissionsService>();
        var snapshot = await permsService.GetAsync(context.HttpContext.RequestAborted);

        var allowed = Permission switch
        {
            Perm.CanCancelWithMoney    => snapshot.CanCancelWithMoney,
            Perm.CanCloseCash          => snapshot.CanCloseCash,
            Perm.CanEditStylists       => snapshot.CanEditStylists,
            Perm.CanEditServices       => snapshot.CanEditServices,
            Perm.CanViewReports        => snapshot.CanViewReports,
            Perm.CanViewCommissions    => snapshot.CanViewCommissions,
            Perm.CanEditSchedule       => snapshot.CanEditSchedule,
            Perm.CanEditPaymentPolicy  => snapshot.CanEditPaymentPolicy,
            Perm.CanEditSalonInfo      => snapshot.CanEditSalonInfo,
            _ => false,
        };

        if (!allowed)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Sin permiso para esta acción",
                Detail = $"La administradora del salón no te dio el permiso '{Permission}'. Pedile que lo active en Configuración → Permisos del equipo.",
                Type = "https://bellasync.app/errors/no-reception-permission",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}

/// <summary>
/// Nombres de los permisos. Constantes para evitar typos en los
/// atributos de cada endpoint.
/// </summary>
public static class Perm
{
    public const string CanCancelWithMoney    = nameof(CanCancelWithMoney);
    public const string CanCloseCash          = nameof(CanCloseCash);
    public const string CanEditStylists       = nameof(CanEditStylists);
    public const string CanEditServices       = nameof(CanEditServices);
    public const string CanViewReports        = nameof(CanViewReports);
    public const string CanViewCommissions    = nameof(CanViewCommissions);
    public const string CanEditSchedule       = nameof(CanEditSchedule);
    public const string CanEditPaymentPolicy  = nameof(CanEditPaymentPolicy);
    public const string CanEditSalonInfo      = nameof(CanEditSalonInfo);
}
