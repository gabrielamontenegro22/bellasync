using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.ReleaseExpiredHolds;

namespace BellaSync.WebApi.HostedServices;

/// <summary>
/// BackgroundService que cada N minutos cancela citas Pending cuyo
/// HoldExpiresAt ya pasó. Sin esto, las citas que el cliente nunca
/// pagó quedarían colgadas indefinidamente, bloqueando el cupo del
/// estilista.
///
/// Diseño:
///  - Corre como hosted service del propio proceso de la API.
///    Mientras la API está arriba, esto está activo. No hace falta
///    cron externo (Windows Task Scheduler, GitHub Actions, etc.).
///  - Cada iteración crea su propio scope porque el handler depende
///    de DbContext (scoped). Sin scope, EF se enojaría.
///  - Idempotente: si dos iteraciones se solapan (que no debería),
///    la segunda no rompe nada — solo encuentra 0 vencidos.
///
/// Frecuencia: 5 minutos es un buen default. Más corto desperdicia
/// queries; más largo deja los cupos bloqueados unos minutos extra.
///
/// Hay también un endpoint manual /api/Internal/release-expired-holds
/// con token para invocarlo desde un cron externo si algún día se
/// quiere mover el job afuera del proceso.
/// </summary>
public sealed class ExpiredHoldsReleaseService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<ExpiredHoldsReleaseService> _logger;

    public ExpiredHoldsReleaseService(
        IServiceProvider services,
        ILogger<ExpiredHoldsReleaseService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ExpiredHoldsReleaseService arrancando — corre cada {Interval}",
            Interval);

        // Esperamos 30s antes del primer ciclo: deja que la app termine
        // de bootstrapearse y evita pegar a la BD durante el startup.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // Loguemos pero NO crasheamos el servicio — si la BD se cayó
                // por un minuto, queremos que el próximo ciclo vuelva a
                // intentar. Solo OperationCanceled rompe el loop.
                _logger.LogError(ex,
                    "Falla en ciclo de ExpiredHoldsReleaseService — reintenta en {Interval}",
                    Interval);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }

        _logger.LogInformation("ExpiredHoldsReleaseService deteniéndose.");
    }

    private async Task ReleaseOnceAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ReleaseExpiredHoldsCommand, ReleaseExpiredHoldsResponse>>();

        var result = await handler.HandleAsync(new ReleaseExpiredHoldsCommand(), ct);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "ReleaseExpiredHolds devolvió error: {Code} {Message}",
                result.Error?.Code, result.Error?.Message);
            return;
        }

        var cancelled = result.Value!.CancelledCount;
        if (cancelled > 0)
        {
            _logger.LogInformation(
                "Liberadas {Count} citas con hold vencido.", cancelled);
        }
    }
}
