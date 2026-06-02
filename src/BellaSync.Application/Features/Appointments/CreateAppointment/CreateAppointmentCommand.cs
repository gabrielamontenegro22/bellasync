using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.CreateAppointment;

/// <summary>
/// Recepción agenda una cita para un cliente conocido.
/// El TenantId se toma del JWT.
///
/// BypassAdvanceWindow: si true, salta la validación de "anticipación mínima"
/// (la regla por defecto de 30 min). Pensado para walk-ins / citas
/// imprevistas — solo SalonAdmin lo puede activar. El controller hace el
/// chequeo de rol antes de pasarlo al handler; cualquier Receptionist que
/// intente mandar el flag verá el flag descartado.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid CustomerId,
    Guid StylistId,
    Guid ServiceId,
    DateTime StartAtUtc,
    string? Notes,
    bool BypassAdvanceWindow = false) : ICommand<AppointmentResponse>;
