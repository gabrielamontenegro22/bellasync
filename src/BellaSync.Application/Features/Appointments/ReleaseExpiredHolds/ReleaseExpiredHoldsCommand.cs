using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Appointments.ReleaseExpiredHolds;

/// <summary>
/// Comando idempotente: encuentra citas Pending con HoldExpiresAt vencido
/// y las cancela. Pensado para ser invocado por un cron job cada N minutos.
/// </summary>
public sealed record ReleaseExpiredHoldsCommand : ICommand<ReleaseExpiredHoldsResponse>;

public sealed record ReleaseExpiredHoldsResponse(int CancelledCount);
