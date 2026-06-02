using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.MarkNoShow;

public sealed record MarkNoShowCommand(Guid Id) : ICommand<AppointmentResponse>;
