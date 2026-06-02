using BellaSync.Application.Auth;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Domain.Entities;
using Microsoft.Extensions.Options;

namespace BellaSync.Application.Features.Auth.Shared;

/// <summary>
/// Centraliza la emisión de access + refresh tokens y la construcción de
/// AuthResponse. Reutilizado por RegisterSalon, Login y RefreshAccessToken.
/// </summary>
public sealed class AuthTokenIssuer
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IClock _clock;
    private readonly JwtSettings _jwtSettings;

    public AuthTokenIssuer(
        IApplicationDbContext db,
        IJwtTokenService jwt,
        IRefreshTokenGenerator refreshTokenGenerator,
        IClock clock,
        IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _jwt = jwt;
        _refreshTokenGenerator = refreshTokenGenerator;
        _clock = clock;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> IssueAsync(
        User user,
        Tenant? tenant,
        string? replacesTokenHash,
        string? createdByIp,
        CancellationToken ct)
    {
        var (accessToken, accessExpiresAt) = _jwt.GenerateToken(user);

        var (refreshPlaintext, refreshHash) = _refreshTokenGenerator.Generate();
        var refreshExpiresAt = _clock.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        var refresh = RefreshToken.Create(
            userId: user.Id,
            tokenHash: refreshHash,
            expiresAtUtc: refreshExpiresAt,
            createdByIp: createdByIp,
            replacesTokenHash: replacesTokenHash);

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse
        {
            Token = accessToken,
            ExpiresAtUtc = accessExpiresAt,
            RefreshToken = refreshPlaintext,
            RefreshTokenExpiresAtUtc = refreshExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            TenantId = user.TenantId,
            TenantName = tenant?.Name ?? string.Empty,
            TenantSlug = tenant?.Slug ?? string.Empty,
        };
    }

    /// <summary>Hashea un refresh token plaintext (lookup en BD).</summary>
    public string HashRefreshToken(string plaintext) =>
        _refreshTokenGenerator.Hash(plaintext);
}
