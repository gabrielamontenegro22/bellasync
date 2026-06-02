using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Appointments.ReleaseExpiredHolds;

/// <summary>
/// Background job: cancela citas Pending cuyo HoldExpiresAt ya pasó.
///
/// Atraviesa el filtro multi-tenant porque corre como tarea de sistema
/// (sin JWT en el contexto). Cada llamada es idempotente: si no hay nada
/// que cancelar, devuelve 0.
/// </summary>
public sealed class ReleaseExpiredHoldsHandler
    : ICommandHandler<ReleaseExpiredHoldsCommand, ReleaseExpiredHoldsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<ReleaseExpiredHoldsHandler> _logger;

    public ReleaseExpiredHoldsHandler(
        IApplicationDbContext db, IClock clock, ILogger<ReleaseExpiredHoldsHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ReleaseExpiredHoldsResponse>> HandleAsync(
        ReleaseExpiredHoldsCommand command, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // IgnoreQueryFilters: corre como sistema, NO filtra por tenant.
        // Filtra solo citas que aún pueden cancelarse (Pending).
        var expired = await _db.Appointments
            .IgnoreQueryFilters()
            .Where(a => a.Status == AppointmentStatus.Pending
                     && a.HoldExpiresAt != null
                     && a.HoldExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return Result<ReleaseExpiredHoldsResponse>.Success(new ReleaseExpiredHoldsResponse(0));

        foreach (var appointment in expired)
        {
            appointment.Cancel(now, reason: "Hold expirado sin validación de anticipo.");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ReleaseExpiredHolds canceló {Count} citas vencidas.", expired.Count);

        return Result<ReleaseExpiredHoldsResponse>.Success(
            new ReleaseExpiredHoldsResponse(expired.Count));
    }
}
