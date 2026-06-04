using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Services;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.WhatsApp;

/// <summary>
/// Helper compartido para que los handlers de Appointment puedan:
///   - Encolar un ConfirmCreated apenas se crea una cita (sin esperar al tick)
///   - Cancelar mensajes Queued cuando una cita se cancela o reagenda
///
/// Se separa del WhatsAppDispatcherService porque el dispatcher es un
/// hosted service que corre solo en WebApi; este helper se inyecta como
/// scoped y lo usan los handlers normalmente.
///
/// Es un "service" en la jerga Clean Architecture: lógica de aplicación
/// que orquesta varias entities/repos pero no es ni command ni query.
/// </summary>
public sealed class WhatsAppEnqueuer
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly WhatsAppTemplateRenderer _renderer;

    public WhatsAppEnqueuer(
        IApplicationDbContext db,
        IClock clock,
        WhatsAppTemplateRenderer renderer)
    {
        _db = db;
        _clock = clock;
        _renderer = renderer;
    }

    /// <summary>
    /// Encola un mensaje del kind dado para una cita YA persistida (el
    /// caller debe hacer SaveChangesAsync para que tanto el appointment
    /// como el mensaje se guarden en la misma transacción).
    ///
    /// Reglas:
    ///   - Si el template del tenant está deshabilitado (o no existe row
    ///     y el catálogo default es OFF), no encola nada y devuelve null.
    ///   - Si ya hay un mensaje Queued o Sent del mismo kind para esta
    ///     cita, NO duplica — idempotente.
    ///   - Si el cliente no tiene teléfono, no encola.
    ///
    /// IgnoreQueryFilters en todas las queries porque este helper puede
    /// llamarse desde el flujo público anónimo (CreatePublicAppointment),
    /// donde el filtro global multi-tenant no tiene un tenant resuelto.
    /// El tenantId que se pasa es explícito.
    /// </summary>
    public async Task<WhatsAppMessage?> EnqueueForAppointmentAsync(
        Guid tenantId,
        Appointment appointment,
        WhatsAppTemplateKind kind,
        CancellationToken ct)
    {
        // 1. Idempotencia: ¿ya existe uno Queued/Sent?
        var existsAlready = await _db.WhatsAppMessages
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId
                        && m.AppointmentId == appointment.Id
                        && m.Kind == kind
                        && (m.Status == WhatsAppMessageStatus.Queued
                            || m.Status == WhatsAppMessageStatus.Sent), ct);
        if (existsAlready) return null;

        // 2. ¿Template está habilitado?
        var catalogEntry = WhatsAppTemplateCatalog.Get(kind);
        var template = await _db.WhatsAppTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Kind == kind, ct);

        var isEnabled = template?.IsEnabled ?? catalogEntry.DefaultEnabled;
        if (!isEnabled) return null;

        // 3. Datos para el render: customer + service + tenant.
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == appointment.CustomerId, ct);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Phone))
            return null;

        var service = await _db.Services
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == appointment.ServiceId, ct);
        if (service is null) return null;

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        // 4. Render + encolar (NO hace SaveChangesAsync — eso es del caller).
        var body = template?.Body ?? catalogEntry.DefaultBody;
        var context = WhatsAppTemplateRenderer.BuildContext(customer, service, appointment, tenant);
        var rendered = _renderer.Render(body, context);

        var msg = WhatsAppMessage.Queue(
            tenantId: tenantId,
            kind: kind,
            customerPhone: customer.Phone,
            renderedBody: rendered,
            appointmentId: appointment.Id,
            utcNow: _clock.UtcNow);

        _db.WhatsAppMessages.Add(msg);
        return msg;
    }

    /// <summary>
    /// Cancela TODOS los mensajes Queued para una cita. Llamado cuando
    /// la cita se cancela (CancelAppointment) o se reagenda (los Queued
    /// estaban armados para la fecha vieja; los nuevos los re-encola el
    /// dispatcher cuando la nueva fecha entre en ventana).
    ///
    /// No hace SaveChangesAsync — el caller debe hacerlo junto con el
    /// cambio de la cita.
    /// </summary>
    public async Task<int> CancelQueuedForAppointmentAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken ct)
    {
        var queued = await _db.WhatsAppMessages
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId
                     && m.AppointmentId == appointmentId
                     && m.Status == WhatsAppMessageStatus.Queued)
            .ToListAsync(ct);

        foreach (var m in queued)
        {
            m.Cancel();
        }
        return queued.Count;
    }
}
