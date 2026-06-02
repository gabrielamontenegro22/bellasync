using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.CancelAppointment;

public sealed record CancelAppointmentCommand(
    Guid Id,
    string? Reason) : ICommand<AppointmentResponse>;
