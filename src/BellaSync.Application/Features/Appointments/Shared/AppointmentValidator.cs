using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Appointments.Shared;

/// <summary>
/// Centraliza las validaciones de negocio comunes para crear una cita.
/// Reutilizado por CreateAppointmentHandler y CreatePublicAppointmentHandler.
///
/// Devuelve Result&lt;ResolvedRefs&gt; con las entidades cargadas si todo OK,
/// o ApplicationError si alguna validación falla.
/// </summary>
public sealed class AppointmentValidator
{
    private readonly IApplicationDbContext _db;

    public AppointmentValidator(IApplicationDbContext db) => _db = db;

    /// <summary>Entidades resueltas y validadas, listas para Appointment.Create.</summary>
    public sealed record ResolvedRefs(Service Service, Stylist Stylist);

    public async Task<Result<ResolvedRefs>> ResolveAndValidateAsync(
        Guid stylistId,
        Guid serviceId,
        DateTime startAtUtc,
        DateTime utcNow,
        int minAdvanceMinutes,
        Guid? excludeAppointmentId,
        CancellationToken ct)
    {
        // 1. Anticipación mínima.
        if (startAtUtc < utcNow.AddMinutes(minAdvanceMinutes))
        {
            return ApplicationError.Validation(
                "appointment.too_soon",
                $"La cita debe agendarse con al menos {minAdvanceMinutes} minutos de anticipación.");
        }

        // 2. Servicio existe y está activo.
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (service is null)
            return ApplicationError.NotFound("service.not_found", "El servicio no existe.");
        if (!service.IsActive)
            return ApplicationError.Validation(
                "appointment.service_inactive",
                "El servicio está archivado y no se puede agendar.");

        // 3. Stylist existe, activo (no Inactive), y puede hacer ese servicio.
        var stylist = await _db.Stylists
            .Include(s => s.StylistServices)
            .FirstOrDefaultAsync(s => s.Id == stylistId, ct);
        if (stylist is null)
            return ApplicationError.NotFound("stylist.not_found", "El estilista no existe.");
        if (stylist.Status == StylistStatus.Inactive)
            return ApplicationError.Validation(
                "appointment.stylist_inactive",
                "El estilista ya no forma parte del equipo.");
        if (stylist.Status == StylistStatus.Vacation)
            return ApplicationError.Validation(
                "appointment.stylist_on_vacation",
                "El estilista está en vacaciones y no toma citas.");

        if (!stylist.StylistServices.Any(ss => ss.ServiceId == serviceId))
        {
            return ApplicationError.Validation(
                "appointment.stylist_cant_do_service",
                $"El estilista {stylist.FullName} no realiza este servicio.");
        }

        // 4. Slot overlap: no puede haber otra cita NO cancelada/no-show
        //    del mismo stylist que se solape con [startAt, startAt + duration).
        var endAtUtc = startAtUtc.AddMinutes(service.DurationMinutes);

        var overlapsQuery = _db.Appointments
            .Where(a => a.StylistId == stylistId
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow
                     && a.StartAt < endAtUtc
                     && a.EndAt > startAtUtc);

        if (excludeAppointmentId is { } excludeId)
            overlapsQuery = overlapsQuery.Where(a => a.Id != excludeId);

        var hasOverlap = await overlapsQuery.AnyAsync(ct);
        if (hasOverlap)
        {
            return ApplicationError.Conflict(
                "appointment.slot_overlap",
                $"El estilista {stylist.FullName} ya tiene una cita en ese horario.");
        }

        return Result<ResolvedRefs>.Success(new ResolvedRefs(service, stylist));
    }
}
