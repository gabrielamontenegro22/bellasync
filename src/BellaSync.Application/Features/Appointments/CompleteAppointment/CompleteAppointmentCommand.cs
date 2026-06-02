using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.CompleteAppointment;

public sealed record CompleteAppointmentCommand(Guid Id) : ICommand<AppointmentResponse>;
