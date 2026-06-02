using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.ConfirmAppointment;

/// <summary>
/// Confirma una cita Pending. Si requería anticipo, también marca el
/// deposit como Validated (asume que la recepción validó el voucher
/// externamente — para flujo completo con voucher ver módulo Vouchers).
/// </summary>
public sealed record ConfirmAppointmentCommand(Guid Id) : ICommand<AppointmentResponse>;
