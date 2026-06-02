using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.CreateAppointment;

/// <summary>
/// Recepción agenda una cita para un cliente conocido.
/// El TenantId se toma del JWT.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid CustomerId,
    Guid StylistId,
    Guid ServiceId,
    DateTime StartAtUtc,
    string? Notes) : ICommand<AppointmentResponse>;
