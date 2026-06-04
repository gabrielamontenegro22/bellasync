using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Users.SetUserActive;

/// <summary>
/// Archiva (IsActive=false) o reactiva (true) un user. No borra
/// hard porque otros records pueden referenciarlo (RefreshTokens,
/// CashClosings.ClosedByUserId, etc.). Archivar evita que pueda
/// loguearse pero preserva la historia.
///
/// Guard: no se puede archivar al último SalonAdmin activo.
/// </summary>
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : ICommand;
