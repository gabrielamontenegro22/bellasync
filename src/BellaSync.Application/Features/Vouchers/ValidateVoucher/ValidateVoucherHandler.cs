using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Application.Features.Vouchers.Shared;
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
    private readonly ILogger<ValidateVoucherHandler> _logger;

    public ValidateVoucherHandler(
        IApplicationDbContext db, IClock clock, ILogger<ValidateVoucherHandler> logger)
    {
        _db = db;
        _clock = clock;
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
                        if (rejectedAppt.Status == AppointmentStatus.Pending
                            || rejectedAppt.Status == AppointmentStatus.Confirmed)
                        {
                            rejectedAppt.Cancel(now, reason);
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

        return Result<VoucherResponse>.Success(VoucherMapper.ToResponse(voucher, now));
    }
}
