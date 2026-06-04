using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Application.Features.Vouchers.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Vouchers.ValidateVoucher;

public sealed class ValidateVoucherHandler
    : ICommandHandler<ValidateVoucherCommand, VoucherResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ILogger<ValidateVoucherHandler> _logger;

    public ValidateVoucherHandler(
        IApplicationDbContext db,
        IClock clock,
        WhatsAppEnqueuer whatsApp,
        ILogger<ValidateVoucherHandler> logger)
    {
        _db = db;
        _clock = clock;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<Result<VoucherResponse>> HandleAsync(
        ValidateVoucherCommand command, CancellationToken ct)
    {
        var voucher = await _db.PaymentVouchers
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .FirstOrDefaultAsync(v => v.Id == command.VoucherId, ct);

        if (voucher is null)
            return ApplicationError.NotFound("voucher.not_found",
                $"No existe el voucher {command.VoucherId}.");

        var now = _clock.UtcNow;

        try
        {
            switch (command.Decision)
            {
                case VoucherDecision.Confirm:
                    voucher.Confirm(command.DecidedByUserId, now, command.Notes);
                    // Cascada al Appointment: validar deposit y confirmar la cita
                    if (voucher.Appointment is { } appt)
                    {
                        appt.ValidateDeposit();
                        appt.Confirm();
                    }
                    break;

                case VoucherDecision.Reject:
                    voucher.Reject(command.DecidedByUserId, now, command.Notes);
                    // Rechazar = "este pago es inválido" → cancelamos la cita
                    // también, libera el cupo. Si la recepcionista solo quería
                    // pedir info adicional, debería usar RequestClarification.
                    // La razón de cancelación incluye la nota para trazabilidad.
                    if (voucher.Appointment is { } rejectedAppt)
                    {
                        var reason = string.IsNullOrWhiteSpace(command.Notes)
                            ? "Pago rechazado en validación."
                            : $"Pago rechazado: {command.Notes}";
                        // Solo cancelamos si la cita está en estado cancelable
                        // (Pending/Confirmed). Si ya está cancelada o terminal,
                        // Cancel() es idempotente o lanza — el catch externo lo maneja.
                        // M11 del audit: si la cita ya fue cancelada por otro
                        // path (ReleaseExpiredHolds o la admin a mano entre que
                        // el voucher llegó y se rechazó), igualmente cancelamos
                        // los WhatsApp Queued para que la cliente no reciba un
                        // recordatorio de una cita rechazada. La cita en sí no
                        // se vuelve a tocar (Cancel sería no-op).
                        if (rejectedAppt.Status == AppointmentStatus.Cancelled
                            || rejectedAppt.Status == AppointmentStatus.NoShow)
                        {
                            _logger.LogWarning(
                                "Voucher {VoucherId} rechazado pero cita {AppointmentId} ya estaba {Status} — solo limpio WhatsApp Queued.",
                                voucher.Id, rejectedAppt.Id, rejectedAppt.Status);
                            await _whatsApp.CancelQueuedForAppointmentAsync(
                                rejectedAppt.TenantId, rejectedAppt.Id, ct);
                        }
                        else if (rejectedAppt.Status == AppointmentStatus.Pending
                            || rejectedAppt.Status == AppointmentStatus.Confirmed)
                        {
                            var wasConfirmed = rejectedAppt.Status == AppointmentStatus.Confirmed;
                            // El user que rechaza el voucher es el responsable
                            // de la cancelación derivada — pasamos su id para que
                            // la auditoría refleje quién canceló (no "Sistema").
                            rejectedAppt.Cancel(now, reason, command.DecidedByUserId);

                            // Bypassear el flujo de CancelAppointmentHandler nos
                            // dejaba sin la propagación a WhatsApp. Replicamos
                            // los dos hooks acá:
                            //   1) Cancelar Reminder24h/Ready2h Queued para no
                            //      mandarle a la cliente "te esperamos" después
                            //      de rechazarle el comprobante.
                            //   2) Notificar la cancelación (solo si estaba
                            //      Confirmed — Pending nunca terminó el flujo
                            //      de agendamiento).
                            await _whatsApp.CancelQueuedForAppointmentAsync(
                                rejectedAppt.TenantId, rejectedAppt.Id, ct);

                            if (wasConfirmed)
                            {
                                await _whatsApp.EnqueueForAppointmentAsync(
                                    tenantId: rejectedAppt.TenantId,
                                    appointment: rejectedAppt,
                                    kind: WhatsAppTemplateKind.AppointmentCancelled,
                                    ct: ct);
                            }
                        }
                    }
                    break;

                case VoucherDecision.RequestClarification:
                    // Aclaración = "necesito más info" → la cita sigue Pending,
                    // el hold sigue corriendo. El cliente puede mandar otro
                    // voucher. Si no lo hace, ReleaseExpiredHolds cancela.
                    voucher.RequestClarification(command.DecidedByUserId, now, command.Notes);
                    break;
            }
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("voucher.invalid_transition", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Voucher {VoucherId} decidido como {Decision} por {UserId}",
            voucher.Id, command.Decision, command.DecidedByUserId);

        // Re-leer con Include de DecidedByUser para que el mapper devuelva
        // el nombre del user que decidió (la nav property no se auto-popula
        // solo por setear el FK DecidedBy en memoria).
        var fresh = await _db.PaymentVouchers
            .AsNoTracking()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .Include(v => v.DecidedByUser)
            .FirstAsync(v => v.Id == voucher.Id, ct);

        return Result<VoucherResponse>.Success(VoucherMapper.ToResponse(fresh, now));
    }
}
