using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.RefreshAccessToken;

/// <summary>
/// Rota un refresh token: lo invalida y emite uno nuevo + un nuevo access.
/// Detección de reuse: si alguien intenta usar un refresh ya revocado, se
/// asume que fue robado y se revoca TODA la cadena del user (defense in depth).
/// </summary>
public sealed class RefreshAccessTokenHandler : ICommandHandler<RefreshAccessTokenCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly ILogger<RefreshAccessTokenHandler> _logger;

    public RefreshAccessTokenHandler(
        IApplicationDbContext db,
        AuthTokenIssuer tokenIssuer,
        ILogger<RefreshAccessTokenHandler> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RefreshAccessTokenCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return ApplicationError.Validation("auth.refresh_required", "Refresh token requerido.");

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

        // DETECCIÓN DE REUSE: el token ya estaba revocado (rotado o explícitamente
        // invalidado). Asumir robo → revocar TODOS los tokens activos del user.
        if (!existing.IsActive())
        {
            _logger.LogWarning(
                "Reuse de refresh token detectado para user {UserId}. Revocando toda la cadena.",
                existing.UserId);

            var allActive = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var t in allActive) t.Revoke();
            await _db.SaveChangesAsync(ct);

            return ApplicationError.Unauthorized(
                "auth.refresh_revoked",
                "Token revocado. Por seguridad se cerraron todas las sesiones.");
        }

        // User/tenant siguen activos
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

        existing.Revoke(replacedByHash: _tokenIssuer.HashRefreshToken(response.RefreshToken));
        await _db.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(response);
    }
}
