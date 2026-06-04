using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Services;
using BellaSync.Application.Features.WhatsApp;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.HostedServices;

/// <summary>
/// Background service que cada 2 minutos:
///
///   1. ENQUEUE: detecta citas en la ventana de cada Reminder y crea
///      WhatsAppMessage rows en estado Queued (con idempotencia: no
///      duplica si ya existe uno Queued/Sent para esa cita+kind).
///
///   2. DISPATCH: levanta los Queued de TODOS los tenants y los manda
///      vía IWhatsAppSender (NoOpWhatsAppSender por default). Marca
///      Sent o Failed según el resultado.
///
/// Triggers implementados:
///   - Reminder24h: cita entre (ahora+23h, ahora+25h)
///   - Ready2h: cita entre (ahora+1h45m, ahora+2h15m)
///
/// Triggers pendientes (próximo sprint):
///   - ConfirmCreated: se enviará al persistir la cita (hook en handler)
///   - PendingDeposit: scan de Pending con hold por vencer
///   - Birthday: scan diario de cumpleaños de hoy
///
/// Frecuencia: 2 min es agresivo pero como las ventanas de tiempo son
/// chicas (~30min), si corremos cada 5min hay riesgo de que una cita
/// "pase de largo" sin recordatorio. 2min da margen.
///
/// Solo procesa templates con IsEnabled=true. La admin puede apagar
/// kinds desde la UI y el dispatcher los saltea sin reiniciar.
/// </summary>
public sealed class WhatsAppDispatcherService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    // Ventanas alrededor del momento de cada recordatorio. Anchas porque
    // el tick es cada 2min y queremos cubrir cualquier cita que caiga
    // entre ticks. La idempotencia previene duplicados.
    private static readonly TimeSpan Reminder24hAheadCenter = TimeSpan.FromHours(24);
    private static readonly TimeSpan Reminder24hHalfWindow = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan Ready2hAheadCenter = TimeSpan.FromHours(2);
    private static readonly TimeSpan Ready2hHalfWindow = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<WhatsAppDispatcherService> _logger;

    public WhatsAppDispatcherService(
        IServiceProvider services,
        ILogger<WhatsAppDispatcherService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WhatsAppDispatcherService arrancando — corre cada {Interval}",
            Interval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);  // dejar bootear
        }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falla en ciclo de WhatsAppDispatcherService — reintenta en {Interval}",
                    Interval);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }

        _logger.LogInformation("WhatsAppDispatcherService deteniéndose.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IApplicationDbContext>();
        var clock = sp.GetRequiredService<IClock>();
        var sender = sp.GetRequiredService<IWhatsAppSender>();
        var renderer = sp.GetRequiredService<WhatsAppTemplateRenderer>();

        var utcNow = clock.UtcNow;

        // ---- ENQUEUE FASE ----
        await EnqueueRemindersAsync(db, renderer, utcNow,
            WhatsAppTemplateKind.Reminder24h,
            Reminder24hAheadCenter, Reminder24hHalfWindow, ct);

        await EnqueueRemindersAsync(db, renderer, utcNow,
            WhatsAppTemplateKind.Ready2h,
            Ready2hAheadCenter, Ready2hHalfWindow, ct);

        // ---- DISPATCH FASE ----
        await DispatchQueuedAsync(db, sender, clock, ct);
    }

    /// <summary>
    /// Busca citas en la ventana de tiempo del recordatorio y encola
    /// mensajes para las que no tengan ya uno persistido (idempotencia).
    /// Salta tenants cuyo template está deshabilitado.
    /// </summary>
    private async Task EnqueueRemindersAsync(
        IApplicationDbContext db,
        WhatsAppTemplateRenderer renderer,
        DateTime utcNow,
        WhatsAppTemplateKind kind,
        TimeSpan aheadCenter,
        TimeSpan halfWindow,
        CancellationToken ct)
    {
        var windowStart = utcNow + aheadCenter - halfWindow;
        var windowEnd = utcNow + aheadCenter + halfWindow;

        // Levantamos las citas elegibles en la ventana, con joins manuales
        // para evitar Includes que traen demasiado. IgnoreQueryFilters
        // porque este service corre sin tenant context (es cross-tenant).
        var candidates = await db.Appointments
            .IgnoreQueryFilters()
            .Where(a => a.StartAt >= windowStart
                     && a.StartAt < windowEnd
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow
                     && a.Status != AppointmentStatus.Completed)
            .Select(a => new
            {
                a.Id,
                a.TenantId,
                a.CustomerId,
                a.ServiceId,
                a.StartAt,
                a.EndAt,
                a.DepositAmount,
                a.HoldExpiresAt,
            })
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var tenantIds = candidates.Select(c => c.TenantId).Distinct().ToList();
        var appointmentIds = candidates.Select(c => c.Id).ToList();
        var customerIds = candidates.Select(c => c.CustomerId).Distinct().ToList();
        var serviceIds = candidates.Select(c => c.ServiceId).Distinct().ToList();

        // Idempotencia: levanta los ya encolados/enviados para estas citas+kind.
        var alreadyExists = await db.WhatsAppMessages
            .IgnoreQueryFilters()
            .Where(m => m.Kind == kind
                     && m.AppointmentId != null
                     && appointmentIds.Contains(m.AppointmentId.Value)
                     && (m.Status == WhatsAppMessageStatus.Queued
                         || m.Status == WhatsAppMessageStatus.Sent))
            .Select(m => m.AppointmentId!.Value)
            .ToListAsync(ct);

        var alreadySet = alreadyExists.ToHashSet();

        // Levantamos en bulk los datos auxiliares (Customer/Service/Tenant/Template).
        var customers = await db.Customers
            .IgnoreQueryFilters()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var services = await db.Services
            .IgnoreQueryFilters()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        // Templates relevantes (puede que no haya row persistida → usar default
        // si el catálogo lo tiene como DefaultEnabled). Si no hay row, asumimos
        // que el tenant no opinó y respetamos el default.
        var templates = await db.WhatsAppTemplates
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.TenantId) && t.Kind == kind)
            .ToDictionaryAsync(t => t.TenantId, ct);

        var catalogEntry = WhatsAppTemplateCatalog.Get(kind);

        var enqueuedThisTick = 0;

        foreach (var c in candidates)
        {
            if (alreadySet.Contains(c.Id)) continue;
            if (!customers.TryGetValue(c.CustomerId, out var customer)) continue;
            if (!services.TryGetValue(c.ServiceId, out var service)) continue;
            if (!tenants.TryGetValue(c.TenantId, out var tenant)) continue;

            // ¿Template está enabled para este tenant?
            var isEnabled = templates.TryGetValue(c.TenantId, out var row)
                ? row.IsEnabled
                : catalogEntry.DefaultEnabled;
            if (!isEnabled) continue;

            // Sin teléfono → saltar. No tiene sentido encolar sin destino.
            if (string.IsNullOrWhiteSpace(customer.Phone)) continue;

            var body = row?.Body ?? catalogEntry.DefaultBody;

            // Materializar mini-appointment para el renderer (no se llevan
            // todas las cosas de la entidad real, alcanza con lo justo).
            var apptForRender = await db.Appointments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == c.Id, ct);
            if (apptForRender is null) continue;

            var context = WhatsAppTemplateRenderer.BuildContext(
                customer, service, apptForRender, tenant);
            var rendered = renderer.Render(body, context);

            var msg = WhatsAppMessage.Queue(
                tenantId: c.TenantId,
                kind: kind,
                customerPhone: customer.Phone,
                renderedBody: rendered,
                appointmentId: c.Id,
                utcNow: utcNow);
            db.WhatsAppMessages.Add(msg);
            enqueuedThisTick++;
        }

        if (enqueuedThisTick > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WhatsApp dispatcher: encoladas {Count} {Kind} en este tick.",
                enqueuedThisTick, kind);
        }
    }

    /// <summary>
    /// Levanta los Queued de todos los tenants y los manda. Si el adapter
    /// devuelve Success → MarkSent; si Failure → MarkFailed con la razón.
    /// </summary>
    private async Task DispatchQueuedAsync(
        IApplicationDbContext db,
        IWhatsAppSender sender,
        IClock clock,
        CancellationToken ct)
    {
        var queued = await db.WhatsAppMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == WhatsAppMessageStatus.Queued)
            .OrderBy(m => m.QueuedAt)
            .Take(100)  // procesar máx 100 por tick para no saturar el sender
            .ToListAsync(ct);

        if (queued.Count == 0) return;

        foreach (var msg in queued)
        {
            if (ct.IsCancellationRequested) break;

            var result = await sender.SendAsync(msg.CustomerPhone, msg.RenderedBody, ct);
            try
            {
                if (result.IsSuccess)
                {
                    msg.MarkSent(result.ExternalMessageId, clock.UtcNow);
                }
                else
                {
                    msg.MarkFailed(result.FailureReason ?? "desconocido", clock.UtcNow);
                }
            }
            catch (BellaSync.Domain.Common.DomainException ex)
            {
                // No debería pasar (filtramos por Queued arriba) pero
                // defensivo: no crasheamos el tick.
                _logger.LogWarning(ex,
                    "No se pudo actualizar estado del mensaje {MessageId}", msg.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "WhatsApp dispatcher: procesados {Count} mensajes en este tick.", queued.Count);
    }
}
