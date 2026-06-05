using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.CancelAppointment;

public sealed class CancelAppointmentHandler
    : ICommandHandler<CancelAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CancelAppointmentHandler> _logger;

    public CancelAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        WhatsAppEnqueuer whatsApp,
        ICurrentUserService currentUser,
        ILogger<CancelAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _whatsApp = whatsApp;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        CancelAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .Include(a => a.CancelledByUser)
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe una cita con id {command.Id}.");

        // Guard configurable: si la cita YA tiene plata cobrada (Payments
        // registrados o vouchers validados), aplicamos las reglas del salón.
        //
        // - Admin: puede cancelar siempre, sin restricciones.
        // - Recepción + tenant.ReceptionCanCancelWithMoney = false:
        //     bloqueado — debe pedirle a admin.
        // - Recepción + tenant.ReceptionCanCancelWithMoney = true:
        //     puede cancelar pero la razón es OBLIGATORIA (auditoría
        //     para que la admin sepa qué hacer con el dinero después).
        if (!_currentUser.IsSalonAdmin)
        {
            var hasMoney = await _db.Payments
                .AsNoTracking()
                .AnyAsync(p => p.AppointmentId == appointment.Id, ct);
            if (!hasMoney)
            {
                hasMoney = await _db.PaymentVouchers
                    .AsNoTracking()
                    .AnyAsync(v => v.AppointmentId == appointment.Id
                                && v.Status == BellaSync.Domain.Entities.PaymentVoucherStatus.Validated, ct);
            }

            if (hasMoney)
            {
                var canCancelWithMoney = await _db.Tenants
                    .AsNoTracking()
                    .Where(t => t.Id == appointment.TenantId)
                    .Select(t => t.ReceptionCanCancelWithMoney)
                    .FirstOrDefaultAsync(ct);

                if (!canCancelWithMoney)
                {
                    return ApplicationError.Forbidden(
                        "appointment.cancel_with_money_requires_admin",
                        "Esta cita ya tiene dinero asociado. La administradora configuró que solo ella puede cancelarlas.");
                }

                // Permitido por el tenant pero exige nota explicativa
                // — admin la verá en la auditoría para decidir refund/crédito.
                if (string.IsNullOrWhiteSpace(command.Reason))
                {
                    return ApplicationError.Validation(
                        "appointment.cancel_with_money_requires_reason",
                        "Esta cita tiene dinero asociado. Tenés que escribir un motivo (qué hacer con el anticipo, si el cliente avisó, etc.) para que la administradora decida después.");
                }
            }
        }

        // Snapshot del status anterior — necesario para decidir si enviar
        // la notificación de cancelación. Una cita Pending que nunca se
        // confirmó no amerita un "lamentamos cancelar tu cita" porque la
        // cliente probablemente nunca terminó el proceso de agendamiento.
        // Solo notificamos cancelación de Confirmed (cita ya en firme).
        var wasConfirmed = appointment.Status == BellaSync.Domain.Entities.AppointmentStatus.Confirmed;

        try { appointment.Cancel(_clock.UtcNow, command.Reason, _currentUser.UserId); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("appointment.invalid_transition", ex.Message);
        }

        // Cancelar WhatsApp Queued de esta cita: si la cancelación ocurre
        // pocas horas antes, el Reminder24h/Ready2h que el dispatcher ya
        // encoló no debería salir (sería absurdo mandar "te esperamos" a
        // una cliente que canceló). Idempotente: si no hay Queued, no-op.
        await _whatsApp.CancelQueuedForAppointmentAsync(
            appointment.TenantId, appointment.Id, ct);

        // Encolar AppointmentCancelled solo si era Confirmed (cita en firme).
        // El helper respeta el toggle isEnabled del template y la idempotencia.
        if (wasConfirmed)
        {
            await _whatsApp.EnqueueForAppointmentAsync(
                tenantId: appointment.TenantId,
                appointment: appointment,
                kind: BellaSync.Domain.Entities.WhatsAppTemplateKind.AppointmentCancelled,
                ct: ct);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} cancelada en tenant {TenantId}",
            appointment.Id, appointment.TenantId);

        // Re-fetch para hidratar CancelledByUser (no auto-popula con solo
        // setear el FK en memoria). Necesario para que el frontend pueda
        // mostrar "Cancelado por X" inmediatamente.
        var fresh = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .Include(a => a.CancelledByUser)
            .FirstAsync(a => a.Id == appointment.Id, ct);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(fresh, _db, ct));
    }
}
