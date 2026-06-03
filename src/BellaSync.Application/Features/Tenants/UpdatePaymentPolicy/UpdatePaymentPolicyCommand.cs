using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdatePaymentPolicy;

/// <summary>
/// Actualiza la política de pagos del tenant actual. Solo SalonAdmin
/// debería invocarlo (el controller lo asegura con [Authorize(Roles)]).
///
/// La validación de rangos vive en Tenant.UpdatePaymentPolicy(...) —
/// si los valores no son razonables (ej. hold de 500 horas), el dominio
/// lanza y el handler lo mapea a Validation.
/// </summary>
public sealed record UpdatePaymentPolicyCommand(
    int HoldDurationHours,
    int HoldMinBeforeAppointmentMinutes,
    int MinAdvanceMinutes
) : ICommand<TenantPaymentPolicyResponse>;
