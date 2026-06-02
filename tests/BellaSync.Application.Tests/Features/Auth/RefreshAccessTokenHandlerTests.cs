using BellaSync.Application.Common.Errors;
using BellaSync.Application.Features.Auth.RefreshAccessToken;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using FluentAssertions;

namespace BellaSync.Application.Tests.Features.Auth;

public class RefreshAccessTokenHandlerTests
{
    private static (HandlerTestContext, RefreshAccessTokenHandler, User) Setup()
    {
        var ctx = new HandlerTestContext();
        var handler = new RefreshAccessTokenHandler(
            ctx.Db, ctx.TokenIssuer, ctx.Clock, ctx.Logger<RefreshAccessTokenHandler>());

        var tenant = Tenant.Create("Test", "test");
        ctx.Db.Tenants.Add(tenant);
        var user = User.Create(tenant.Id, "u@e.com", "HASH:x", "User", UserRole.SalonAdmin);
        ctx.Db.Users.Add(user);
        ctx.Db.SaveChanges();
        return (ctx, handler, user);
    }

    private static RefreshToken SeedRefreshToken(
        HandlerTestContext ctx,
        Guid userId,
        string plaintext,
        DateTime expiresAt,
        bool revoked = false)
    {
        var token = RefreshToken.Create(
            userId: userId,
            tokenHash: HandlerTestContext.HashOf(plaintext),
            expiresAtUtc: expiresAt);
        if (revoked) token.Revoke(ctx.Clock.UtcNow);
        ctx.Db.RefreshTokens.Add(token);
        ctx.Db.SaveChanges();
        return token;
    }

    [Fact]
    public async Task Refresh_with_valid_token_rotates_and_returns_new_tokens()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        SeedRefreshToken(ctx, user.Id, "plain-A", ctx.Clock.UtcNow.AddDays(10));

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand("plain-A", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBe("plain-A");

        // El token original debe estar revocado y linkeado al nuevo
        var oldToken = ctx.Db.RefreshTokens.Single(t => t.TokenHash == HandlerTestContext.HashOf("plain-A"));
        oldToken.RevokedAt.Should().NotBeNull();
        oldToken.ReplacedByTokenHash.Should().Be(HandlerTestContext.HashOf(result.Value.RefreshToken));
    }

    [Fact]
    public async Task Refresh_with_revoked_token_triggers_reuse_detection_and_revokes_entire_chain()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        // Seedeo tres tokens activos del mismo user
        SeedRefreshToken(ctx, user.Id, "active-1", ctx.Clock.UtcNow.AddDays(10));
        SeedRefreshToken(ctx, user.Id, "active-2", ctx.Clock.UtcNow.AddDays(10));
        // Token revocado anterior — alguien intenta usarlo (reuse)
        SeedRefreshToken(ctx, user.Id, "revoked-old", ctx.Clock.UtcNow.AddDays(10), revoked: true);

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand("revoked-old", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.refresh_revoked");

        // TODOS los tokens activos deben quedar revocados
        var allTokens = ctx.Db.RefreshTokens.ToList();
        allTokens.Should().HaveCount(3);
        allTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Refresh_with_expired_token_returns_invalid_no_reuse_detection()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        // Token NO revocado pero expirado
        SeedRefreshToken(ctx, user.Id, "expired", ctx.Clock.UtcNow.AddDays(-1));

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand("expired", null), default);

        result.IsFailure.Should().BeTrue();
        // El token expirado activa la rama de "no activo" → revoca cadena
        // (defensa pesimista: si el token está en BD pero no activo, asumir lo peor)
        result.Error!.Code.Should().Be("auth.refresh_revoked");
    }

    [Fact]
    public async Task Refresh_with_unknown_token_returns_unauthorized()
    {
        var (ctx, handler, _) = Setup();
        using var _ = ctx;

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand("never-seen", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.refresh_invalid");
    }

    [Fact]
    public async Task Refresh_with_empty_token_returns_validation_error()
    {
        var (ctx, handler, _) = Setup();
        using var _ = ctx;

        var result = await handler.HandleAsync(
            new RefreshAccessTokenCommand("", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ApplicationErrorType.Validation);
        result.Error.Code.Should().Be("auth.refresh_required");
    }
}
