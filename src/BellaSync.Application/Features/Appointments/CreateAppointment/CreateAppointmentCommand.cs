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
///
/// ApplyCreditFromVoucherIds: lista opcional de vouchers CreditPending del
/// mismo cliente para aplicar como anticipo de esta cita. El handler valida
/// que la suma de créditos cubra el anticipo requerido — si no alcanza,
/// rechaza con mensaje claro. Los vouchers se consumen FIFO (más antiguos
/// primero); el sobrante queda disponible para futuras aplicaciones.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid CustomerId,
    Guid StylistId,
    Guid ServiceId,
    DateTime StartAtUtc,
    string? Notes,
    bool BypassAdvanceWindow = false,
    IReadOnlyList<Guid>? ApplyCreditFromVoucherIds = null) : ICommand<AppointmentResponse>;
