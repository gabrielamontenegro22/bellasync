using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.CreateAppointment;

public sealed class CreateAppointmentHandler : ICommandHandler<CreateAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly SalonScheduleValidator _scheduleValidator;
    private readonly ITenantAppointmentSettings _settings;
    private readonly WhatsAppEnqueuer _whatsApp;
    private readonly ILogger<CreateAppointmentHandler> _logger;

    public CreateAppointmentHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ICurrentUserService currentUser,
        IClock clock,
        AppointmentValidator validator,
        SalonScheduleValidator scheduleValidator,
        ITenantAppointmentSettings settings,
        WhatsAppEnqueuer whatsApp,
        ILogger<CreateAppointmentHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _clock = clock;
        _validator = validator;
        _scheduleValidator = scheduleValidator;
        _settings = settings;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<Result<AppointmentResponse>> HandleAsync(
        CreateAppointmentCommand command, CancellationToken ct)
    {
        // Cliente existe en este tenant (el filtro multi-tenant lo asegura).
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);
        if (customer is null)
            return ApplicationError.NotFound("customer.not_found", "El cliente no existe.");
        if (!customer.IsActive)
            return ApplicationError.Validation("customer.inactive",
                "El cliente está archivado.");

        // Resolver y validar service + stylist + overlap.
        // Si bypass activo (walk-in autorizado por admin), pasamos minAdvance=0
        // para que el validator no rechace por "muy próximo". El factory del
        // dominio sigue rechazando startAt en el pasado, así que el peor caso
        // es agendar a "ahora mismo + 1 segundo".
        var minAdvance = command.BypassAdvanceWindow ? 0 : await _settings.GetMinAdvanceMinutesAsync(ct);
        var holdHours = await _settings.GetHoldDurationHoursAsync(ct);
        var holdMinBefore = await _settings.GetHoldMinBeforeAppointmentMinutesAsync(ct);

        var refsResult = await _validator.ResolveAndValidateAsync(
            stylistId: command.StylistId,
            serviceId: command.ServiceId,
            startAtUtc: command.StartAtUtc,
            utcNow: _clock.UtcNow,
            minAdvanceMinutes: minAdvance,
            excludeAppointmentId: null,
            ct: ct);

        if (refsResult.IsFailure) return refsResult.Error!;

        var refs = refsResult.Value!;
        var endAtUtc = command.StartAtUtc.AddMinutes(refs.Service.DurationMinutes);

        // Validar que la franja cae dentro del horario configurado por
        // el salón (día abierto, dentro del rango, no en lunch, no en
        // cierre puntual, no en festivo). El mismo flag de bypass que
        // permite walk-ins se honra acá — la admin puede meter un
        // walk-in fuera de hora si lo necesita.
        var scheduleResult = await _scheduleValidator.ValidateAsync(
            tenantId: _currentTenant.TenantId,
            startUtc: command.StartAtUtc,
            endUtc: endAtUtc,
            bypass: command.BypassAdvanceWindow,
            ct: ct);
        if (scheduleResult.IsFailure) return scheduleResult.Error!;

        var appointment = Appointment.Create(
            tenantId: _currentTenant.TenantId,
            customerId: customer.Id,
            stylistId: refs.Stylist.Id,
            serviceId: refs.Service.Id,
            startAtUtc: command.StartAtUtc,
            endAtUtc: endAtUtc,
            priceSnapshot: refs.Service.Price,
            depositPercentage: refs.Service.DepositPercentage,
            requiresDeposit: refs.Service.RequiresDeposit,
            channel: AppointmentChannel.Reception,
            notes: command.Notes,
            utcNow: _clock.UtcNow,
            holdDuration: TimeSpan.FromHours(holdHours),
            holdMinBeforeAppointment: TimeSpan.FromMinutes(holdMinBefore));

        _db.Appointments.Add(appointment);

        // Aplicar crédito si la admin/recepción lo seleccionó en el modal.
        // Solo válido para citas que efectivamente exigen anticipo — un
        // servicio sin requiresDeposit no tiene a qué aplicar el crédito.
        if (command.ApplyCreditFromVoucherIds is { Count: > 0 } voucherIds
            && appointment.DepositAmount.Amount > 0m)
        {
            var creditResult = await ApplyCustomerCreditsAsync(
                appointment, customer, voucherIds, ct);
            if (creditResult.IsFailure) return creditResult.Error!;
        }

        // ConfirmCreated WhatsApp: encolar para que salga al instante en
        // el próximo tick del dispatcher (~2min). Sin esto, el dispatcher
        // solo arma Reminder24h/Ready2h por ventana de tiempo, y la
        // confirmación de agendamiento se perdería para citas a >25h.
        // Se hace en la misma transacción (SaveChangesAsync abajo persiste
        // appointment + mensaje juntos).
        await _whatsApp.EnqueueForAppointmentAsync(
            tenantId: _currentTenant.TenantId,
            appointment: appointment,
            kind: WhatsAppTemplateKind.ConfirmCreated,
            ct: ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cita {AppointmentId} creada en tenant {TenantId} (status={Status})",
            appointment.Id, appointment.TenantId, appointment.Status);

        // Releer con includes para que el mapper tenga acceso a las navigations.
        var created = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.Service)
            .FirstAsync(a => a.Id == appointment.Id, ct);

        return Result<AppointmentResponse>.Success(
            await AppointmentMapper.ToResponseAsync(created, _db, ct));
    }

    /// <summary>
    /// Aplica créditos pendientes del cliente al anticipo de la nueva cita.
    /// Para v1, la regla es estricta: la suma de créditos seleccionados
    /// DEBE cubrir el anticipo requerido. Si no alcanza, rechaza con
    /// mensaje claro. Esto evita el caso ambiguo "cubro parcial y dejo el
    /// resto pendiente" que requiere lógica de vouchers parciales en la
    /// cola de validación (deferrable para v2).
    ///
    /// El consumo es FIFO (más antiguos primero) — protege contra que
    /// vouchers queden estancados con saldos pequeños sin usar nunca.
    /// El sobrante de cada voucher sigue disponible para futuras
    /// aplicaciones (gracias a AmountApplied).
    ///
    /// Crea un voucher Validated nuevo en la cita por el monto del anticipo
    /// (no por la suma de créditos consumidos — si se consumió de más, eso
    /// queda en el saldo de los vouchers originales). Confirma la cita
    /// automáticamente porque el anticipo queda cubierto.
    /// </summary>
    private async Task<Result<bool>> ApplyCustomerCreditsAsync(
        Appointment appointment,
        Customer customer,
        IReadOnlyList<Guid> voucherIds,
        CancellationToken ct)
    {
        var vouchers = await _db.PaymentVouchers
            .Include(v => v.Appointment)
            .Where(v => voucherIds.Contains(v.Id))
            .ToListAsync(ct);

        if (vouchers.Count != voucherIds.Count)
            return ApplicationError.Validation("credit.not_found",
                "Uno o más créditos seleccionados no existen.");

        // Todos los vouchers tienen que ser del mismo cliente (no permitir
        // aplicar créditos de otra cliente). El global filter ya restringió
        // por tenant — acá solo validamos cliente.
        if (vouchers.Any(v => v.Appointment is null || v.Appointment.CustomerId != customer.Id))
            return ApplicationError.Validation("credit.wrong_customer",
                "Alguno de los créditos no pertenece a este cliente.");

        // Todos deben ser CreditPending con saldo positivo. AvailableCredit
        // ya combina las dos verificaciones (status + decision + resolved).
        if (vouchers.Any(v => v.AvailableCredit <= 0m))
            return ApplicationError.Validation("credit.not_available",
                "Alguno de los créditos seleccionados ya no está disponible.");

        var totalAvailable = vouchers.Sum(v => v.AvailableCredit);
        var depositRequired = appointment.DepositAmount.Amount;

        if (totalAvailable < depositRequired)
            return ApplicationError.Validation("credit.insufficient",
                $"El crédito disponible (${totalAvailable:N0}) no cubre el anticipo requerido (${depositRequired:N0}). " +
                "Agendá un servicio con anticipo menor o aplicá el crédito en sumar al pago.");

        // Consumo FIFO hasta cubrir el anticipo exacto.
        var remaining = depositRequired;
        var now = _clock.UtcNow;
        var userId = _currentUser.UserId ?? Guid.Empty;

        foreach (var v in vouchers.OrderBy(v => v.Appointment!.CancelledAt ?? v.ReceivedAt))
        {
            if (remaining <= 0m) break;
            var toApply = Math.Min(v.AvailableCredit, remaining);
            try
            {
                v.ApplyCredit(toApply, now, userId);
            }
            catch (Domain.Common.DomainException ex)
            {
                return ApplicationError.Validation("credit.apply_failed", ex.Message);
            }
            remaining -= toApply;
        }

        // Crear voucher Validated nuevo en la cita por el monto del anticipo,
        // marcado con IsInternalCredit=true para que el resto del sistema
        // sepa que NO representa plata nueva entrando ese día. Este flag
        // tiene consecuencias en Caja, Cancel y MarkRefundResolved.
        var creditVoucher = PaymentVoucher.CreateInternalCredit(
            tenantId: _currentTenant.TenantId,
            appointmentId: appointment.Id,
            amount: appointment.DepositAmount,
            customerNameForDisplay: customer.FullName,
            customerPhoneForDisplay: customer.Phone,
            utcNow: now);
        // Confirm inmediato — no pasa por cola de validación porque el
        // dinero "real" entró en el voucher original que ya fue validado.
        creditVoucher.Confirm(userId, now,
            "Aplicación automática de crédito de cita cancelada previamente.");
        _db.PaymentVouchers.Add(creditVoucher);

        // Estado de la cita: el anticipo queda cubierto → validar deposit
        // y confirmar la cita (libera el hold). Si la cita no requería
        // anticipo (raro acá porque ya filtramos por DepositAmount>0), el
        // ValidateDeposit lanza — lo capturamos por las dudas.
        try
        {
            appointment.ValidateDeposit();
            appointment.Confirm();
        }
        catch (Domain.Common.DomainException ex)
        {
            return ApplicationError.Validation("credit.confirm_failed", ex.Message);
        }

        _logger.LogInformation(
            "Crédito aplicado a cita {AppointmentId}: ${Amount:N0} desde {VoucherCount} voucher(s) del cliente {CustomerId}",
            appointment.Id, depositRequired, vouchers.Count, customer.Id);

        return Result<bool>.Success(true);
    }
}
