using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Application.Features.Vouchers.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Vouchers.CreateVoucher;

public sealed class CreateVoucherHandler : ICommandHandler<CreateVoucherCommand, VoucherResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<CreateVoucherHandler> _logger;

    public CreateVoucherHandler(
        IApplicationDbContext db, IClock clock, ILogger<CreateVoucherHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<VoucherResponse>> HandleAsync(
        CreateVoucherCommand command, CancellationToken ct)
    {
        // IgnoreQueryFilters: el webhook futuramente correrá sin tenant context
        // y aún así necesita encontrar la cita por id. La cita "carga" su tenant.
        var appointment = await _db.Appointments
            .IgnoreQueryFilters()
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.Stylist)
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId, ct);

        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found",
                $"No existe la cita {command.AppointmentId}.");

        var voucher = PaymentVoucher.Create(
            tenantId: appointment.TenantId,
            appointmentId: appointment.Id,
            reportedAmount: Money.Create(command.ReportedAmount),
            bank: command.Bank,
            referenceNumber: command.ReferenceNumber,
            senderName: command.SenderName,
            senderPhone: command.SenderPhone,
            imageUrl: command.ImageUrl,
            utcNow: _clock.UtcNow);

        _db.PaymentVouchers.Add(voucher);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Voucher {VoucherId} creado para cita {AppointmentId} en tenant {TenantId}",
            voucher.Id, appointment.Id, appointment.TenantId);

        // El mapper necesita el Appointment ya cargado — usamos el que tenemos
        voucher = await _db.PaymentVouchers
            .IgnoreQueryFilters()
            .Include(v => v.Appointment).ThenInclude(a => a!.Customer)
            .Include(v => v.Appointment).ThenInclude(a => a!.Service)
            .Include(v => v.Appointment).ThenInclude(a => a!.Stylist)
            .FirstAsync(v => v.Id == voucher.Id, ct);

        return Result<VoucherResponse>.Success(VoucherMapper.ToResponse(voucher, _clock.UtcNow));
    }
}
