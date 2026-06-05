using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
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
    private readonly IReceptionPermissionsService _perms;
    private readonly ITenantAppointmentSettings _settings;
    private readonly ILogger<CancelAppointmentHandler> _logger;

    public CancelAppointmentHandler(
        IApplicationDbContext db,
        IClock clock,
        WhatsAppEnqueuer whatsApp,
        ICurrentUserService currentUser,
        IReceptionPermissionsService perms,
        ITenantAppointmentSettings settings,
        ILogger<CancelAppointmentHandler> logger)
    {
        _db = db;
        _clock = clock;
        _whatsApp = whatsApp;
        _currentUser = currentUser;
        _perms = perms;
        _settings = settings;
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

        // Vouchers validados de esta cita: si los hay, hay anticipo cobrado
        // y la cancelación dispara la decisión de refund. Solo cargamos
        // los Validated — Pending/Rejected/NeedsClarification no tienen
        // dinero "del salón" para devolver.
        var validatedVouchers = await _db.PaymentVouchers
            .Where(v => v.AppointmentId == appointment.Id
                     && v.Status == PaymentVoucherStatus.Validated)
            .ToListAsync(ct);

        var hasValidatedDeposit = validatedVouchers.Count > 0;

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
            var hasMoney = hasValidatedDeposit || await _db.Payments
                .AsNoTracking()
                .AnyAsync(p => p.AppointmentId == appointment.Id, ct);

            if (hasMoney)
            {
                // Snapshot cacheado por request (IReceptionPermissionsService).
                // Para el primer chequeo del request hace 1 query a tenants;
                // chequeos siguientes son in-memory.
                var perms = await _perms.GetAsync(ct);
                if (!perms.CanCancelWithMoney)
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

                // Si recepción mandó override de la decisión de refund,
                // necesita el permiso CanRefundDeposit. La admin nunca
                // pasa por este branch (IsSalonAdmin = true arriba).
                if (command.DepositOverride is not null && !perms.CanRefundDeposit)
                {
                    return ApplicationError.Forbidden(
                        "appointment.refund_override_requires_permission",
                        "No tenés permiso para elegir qué hacer con el anticipo. La administradora configuró que solo ella decide refunds.");
                }
            }
        }

        // Snapshot del status anterior — necesario para decidir si enviar
        // la notificación de cancelación. Una cita Pending que nunca se
        // confirmó no amerita un "lamentamos cancelar tu cita" porque la
        // cliente probablemente nunca terminó el proceso de agendamiento.
        // Solo notificamos cancelación de Confirmed (cita ya en firme).
        var wasConfirmed = appointment.Status == AppointmentStatus.Confirmed;

        try { appointment.Cancel(_clock.UtcNow, command.Reason, _currentUser.UserId); }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("appointment.invalid_transition", ex.Message);
        }

        // === Registrar la decisión de refund en cada voucher validado ===
        // La regla automática: dentro de la ventana del salón → Refunded;
        // fuera → Forfeited. El override del comando (si vino) tiene prio.
        if (hasValidatedDeposit)
        {
            var decision = await ResolveDecisionAsync(appointment, command.DepositOverride, ct);
            var decidedBy = _currentUser.UserId ?? Guid.Empty;
            foreach (var voucher in validatedVouchers)
            {
                try { voucher.RecordRefundDecision(decision, _clock.UtcNow, decidedBy); }
                catch (DomainException ex)
                {
                    // Caso borde: voucher ya tenía decisión registrada
                    // (puede pasar si dos requests cancelan en paralelo).
                    // Logueamos y seguimos — la cita ya está cancelada,
                    // no queremos rollback por esto.
                    _logger.LogWarning(ex,
                        "No se pudo registrar refund decision en voucher {VoucherId}",
                        voucher.Id);
                }
            }
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
                kind: WhatsAppTemplateKind.AppointmentCancelled,
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

    /// <summary>
    /// Devuelve la decisión final de refund: override del comando si vino
    /// y es legal, regla automática (ventana del tenant) si no.
    ///
    /// Semántica de la ventana:
    ///   - windowHours = 0  → política estricta: el anticipo nunca se
    ///     devuelve automáticamente. La admin/recepción con permiso
    ///     puede igual override desde el modal.
    ///   - windowHours > 0  → si cancela con esa anticipación o más,
    ///     devuelve (Refunded); si cancela más sobre la hora, pierde
    ///     (Forfeited).
    /// </summary>
    private async Task<DepositRefundDecision> ResolveDecisionAsync(
        Domain.Entities.Appointment appointment,
        DepositRefundDecision? overrideDecision,
        CancellationToken ct)
    {
        if (overrideDecision is not null)
            return overrideDecision.Value;

        var windowHours = await _settings.GetCancellationWindowHoursAsync(ct);

        // Ventana 0 = política estricta. Sin esta guarda, "hoursUntil >= 0"
        // (cita aún no pasó) caería en Refunded, que es lo contrario a lo
        // que la admin pidió al configurar 0.
        if (windowHours <= 0)
            return DepositRefundDecision.Forfeited;

        var hoursUntilAppointment = (appointment.StartAt - _clock.UtcNow).TotalHours;

        // >= ventana → dentro del plazo → devolver.
        // <  ventana → fuera del plazo → perdido.
        return hoursUntilAppointment >= windowHours
            ? DepositRefundDecision.Refunded
            : DepositRefundDecision.Forfeited;
    }
}
