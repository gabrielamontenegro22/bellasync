using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.MarkInProgress;

public sealed record MarkInProgressCommand(Guid Id) : ICommand<AppointmentResponse>;
