using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Payments.Dtos;
using BellaSync.Application.Features.Payments.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Payments.RegisterPayment;

public sealed class RegisterPaymentHandler
    : ICommandHandler<RegisterPaymentCommand, PaymentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<RegisterPaymentHandler> _logger;

    public RegisterPaymentHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<RegisterPaymentHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<PaymentResponse>> HandleAsync(
        RegisterPaymentCommand command, CancellationToken ct)
    {
        // 1. La cita debe existir en este tenant (multi-tenant filter ya filtra).
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId, ct);
        if (appointment is null)
            return ApplicationError.NotFound("appointment.not_found", "La cita no existe.");

        // 2. Solo se registran pagos a citas que ya iniciaron o terminaron.
        //    Pending/Confirmed → la cliente aún no llegó: no debería estar
        //    pagando aún (si va a pagar anticipo, eso es Voucher, no Payment).
        //    Cancelled/NoShow  → no se cobró nada.
        var valid = appointment.Status == AppointmentStatus.InProgress
                 || appointment.Status == AppointmentStatus.Completed;
        if (!valid)
        {
            return ApplicationError.Validation(
                "payment.appointment_not_started",
                $"No se puede registrar un pago de una cita en estado {appointment.Status}. " +
                "La cita debe estar en curso o completada.");
        }

        // 3. Construir el Money. Money.Create valida >= 0 — un Amount=0 lo
        //    rechaza el factory de Payment.
        Money amount, tip;
        try
        {
            amount = Money.Create(command.Amount);
            tip = Money.Create(command.Tip);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("payment.invalid_amount", ex.Message);
        }

        // 3.5. Validar que no se sobre-pague. Saldo = precio del servicio
        //      − anticipos validados − pagos ya registrados. La propina
        //      (tip) NO cuenta para el saldo: es plata extra que va al
        //      estilista, no parte del cobro del servicio.
        //
        //      Suma en memoria porque Money/HasConversion no traduce bien
        //      en SUM() de SQL (mismo problema que tuvimos antes).
        var alreadyValidatedDeposits = await _db.PaymentVouchers
            .AsNoTracking()
            .Where(v => v.AppointmentId == appointment.Id
                     && v.Status == PaymentVoucherStatus.Validated)
            .Select(v => v.ReportedAmount)
            .ToListAsync(ct);
        var alreadyPaidPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.AppointmentId == appointment.Id)
            .Select(p => p.Amount)
            .ToListAsync(ct);

        var alreadyPaid = alreadyValidatedDeposits.Sum(m => m.Amount)
                        + alreadyPaidPayments.Sum(m => m.Amount);
        var remaining = appointment.PriceSnapshot.Amount - alreadyPaid;

        if (amount.Amount > remaining)
        {
            return ApplicationError.Validation(
                "payment.exceeds_remaining",
                $"El monto ({amount.Amount:N0}) supera el saldo pendiente ({remaining:N0}). " +
                $"Servicio: {appointment.PriceSnapshot.Amount:N0}, ya pagado: {alreadyPaid:N0}. " +
                "Si la cliente quiere dar extra, va como propina.");
        }

        // 4. Crear el pago vía factory del dominio (que valida invariantes adicionales).
        Payment payment;
        try
        {
            payment = Payment.Create(
                tenantId: _currentTenant.TenantId,
                appointmentId: appointment.Id,
                method: command.Method,
                provider: command.Provider,
                amount: amount,
                tip: tip,
                reference: command.Reference,
                registeredByUserId: command.RegisteredByUserId,
                utcNow: _clock.UtcNow);
        }
        catch (BellaSync.Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("payment.invalid", ex.Message);
        }

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pago {PaymentId} registrado para cita {AppointmentId} en tenant {TenantId} ({Method} ${Amount})",
            payment.Id, payment.AppointmentId, payment.TenantId, payment.Method, payment.Amount.Amount);

        // 5. Re-leer con includes para que el mapper tenga service+stylist
        //    + nombre del user que cobró (auditoría en /caja).
        var created = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Service)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Stylist)
            .Include(p => p.Appointment)!.ThenInclude(a => a!.Customer)
            .Include(p => p.RegisteredByUser)
            .FirstAsync(p => p.Id == payment.Id, ct);

        return Result<PaymentResponse>.Success(PaymentMapper.ToResponse(created));
    }
}
