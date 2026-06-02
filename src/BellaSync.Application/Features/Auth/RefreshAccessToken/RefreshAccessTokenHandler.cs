using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenHandler : ICommandHandler<RefreshAccessTokenCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly IClock _clock;
    private readonly ILogger<RefreshAccessTokenHandler> _logger;

    public RefreshAccessTokenHandler(
        IApplicationDbContext db,
        AuthTokenIssuer tokenIssuer,
        IClock clock,
        ILogger<RefreshAccessTokenHandler> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RefreshAccessTokenCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return ApplicationError.Validation("auth.refresh_required", "Refresh token requerido.");

        var now = _clock.UtcNow;
        var hash = _tokenIssuer.HashRefreshToken(command.RefreshToken);

        var existing = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
                .ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
        {
            return ApplicationError.Unauthorized("auth.refresh_invalid", "Refresh token inválido.");
        }

        // DETECCIÓN DE REUSE: token revocado pero alguien lo intenta usar.
        // Asumir robo → revocar TODOS los tokens activos del user.
        if (!existing.IsActive(now))
        {
            _logger.LogWarning(
                "Reuse de refresh token detectado para user {UserId}. Revocando toda la cadena.",
                existing.UserId);

            var allActive = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var t in allActive) t.Revoke(now);
            await _db.SaveChangesAsync(ct);

            return ApplicationError.Unauthorized(
                "auth.refresh_revoked",
                "Token revocado. Por seguridad se cerraron todas las sesiones.");
        }

        if (!existing.User.IsActive || existing.User.Tenant is { IsActive: false })
        {
            return ApplicationError.Unauthorized(
                "auth.session_invalid",
                "La cuenta ya no está activa.");
        }

        // Rotar: emitir nuevo refresh + revocar el actual con linkeo a la cadena
        var response = await _tokenIssuer.IssueAsync(
            user: existing.User,
            tenant: existing.User.Tenant,
            replacesTokenHash: hash,
            createdByIp: command.CreatedByIp,
            ct: ct);

        existing.Revoke(now, replacedByHash: _tokenIssuer.HashRefreshToken(response.RefreshToken));
        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(response);
    }
}
