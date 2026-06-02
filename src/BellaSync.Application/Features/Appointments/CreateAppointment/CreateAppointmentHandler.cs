using BellaSync.Application.Auth;
using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BellaSync.Application.Features.Appointments.CreateAppointment;

public sealed class CreateAppointmentHandler : ICommandHandler<CreateAppointmentCommand, AppointmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly AppointmentValidator _validator;
    private readonly AppointmentSettings _settings;
    private readonly ILogger<CreateAppointmentHandler> _logger;

    public CreateAppointmentHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        AppointmentValidator validator,
        IOptions<AppointmentSettings> settings,
        ILogger<CreateAppointmentHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _validator = validator;
        _settings = settings.Value;
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
        var refsResult = await _validator.ResolveAndValidateAsync(
            stylistId: command.StylistId,
            serviceId: command.ServiceId,
            startAtUtc: command.StartAtUtc,
            utcNow: _clock.UtcNow,
            minAdvanceMinutes: _settings.MinAdvanceMinutes,
            excludeAppointmentId: null,
            ct: ct);

        if (refsResult.IsFailure) return refsResult.Error!;

        var refs = refsResult.Value!;
        var endAtUtc = command.StartAtUtc.AddMinutes(refs.Service.DurationMinutes);

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
            holdDuration: TimeSpan.FromHours(_settings.HoldDurationHours),
            holdMinBeforeAppointment: TimeSpan.FromMinutes(_settings.HoldMinBeforeAppointmentMinutes));

        _db.Appointments.Add(appointment);
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

        return Result<AppointmentResponse>.Success(AppointmentMapper.ToResponse(created));
    }
}
