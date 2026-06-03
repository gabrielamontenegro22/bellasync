using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;

namespace BellaSync.Application.Features.Appointments.RescheduleAppointment;

/// <summary>
/// Reagenda una cita existente a un nuevo horario. Mantiene customer/stylist/
/// service idénticos — si la cliente quiere cambiar de estilista o servicio,
/// se cancela y se crea una nueva.
///
/// El handler valida overlap excluyendo la propia cita y respeta la regla
/// de anticipación mínima (configurable y bypaseable solo por SalonAdmin
/// para casos imprevistos).
/// </summary>
public sealed record RescheduleAppointmentCommand(
    Guid Id,
    DateTime NewStartAtUtc,
    bool BypassAdvanceWindow = false
) : ICommand<AppointmentResponse>;
