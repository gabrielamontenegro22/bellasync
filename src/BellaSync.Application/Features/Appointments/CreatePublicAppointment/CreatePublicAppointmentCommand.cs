using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.CreatePublicAppointment;

/// <summary>
/// Cliente del portal público agenda una cita.
/// TenantSlug identifica el salón en la URL (ej. /booking/bella-spa).
/// El handler crea Customer automáticamente si el teléfono es nuevo.
/// </summary>
public sealed record CreatePublicAppointmentCommand(
    string TenantSlug,
    Guid StylistId,
    Guid ServiceId,
    DateTime StartAtUtc,
    string ClientName,
    string ClientPhone,
    string? ClientEmail) : ICommand<PublicBookingResponse>;
