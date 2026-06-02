using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.GetAppointment;

public sealed record GetAppointmentQuery(Guid Id) : IQuery<AppointmentResponse>;
