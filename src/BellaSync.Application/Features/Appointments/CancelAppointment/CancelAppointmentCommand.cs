using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Appointments.CancelAppointment;

/// <summary>
/// Cancela una cita y, si tenía anticipo Validado, registra qué pasa con
/// el dinero. La regla automática se aplica si <see cref="DepositOverride"/>
/// es null:
///   - Cancela dentro de la ventana del salón → Refunded.
///   - Cancela fuera de la ventana → Forfeited.
///
/// El override solo lo puede mandar admin (siempre) o recepción si la
/// admin activó <c>CanRefundDeposit</c>. Si recepción manda override sin
/// permiso, el handler responde 403.
/// </summary>
public sealed record CancelAppointmentCommand(
    Guid Id,
    string? Reason,
    DepositRefundDecision? DepositOverride) : ICommand<AppointmentResponse>;
