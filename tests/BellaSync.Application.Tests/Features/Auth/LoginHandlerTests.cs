using BellaSync.Application.Common.Errors;
using BellaSync.Application.Features.Auth.Login;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Tests.Features.Auth;

public class LoginHandlerTests
{
    private static (HandlerTestContext, LoginHandler) Setup()
    {
        var ctx = new HandlerTestContext();
        var handler = new LoginHandler(ctx.Db, ctx.PasswordHasher, ctx.TokenIssuer, ctx.Clock);
        return (ctx, handler);
    }

    private static User SeedUser(
        HandlerTestContext ctx,
        string email = "test@example.com",
        string plainPassword = "Password123",
        bool isActive = true,
        Tenant? tenant = null)
    {
        tenant ??= Tenant.Create("Test Salon", "test-salon");
        ctx.Db.Tenants.Add(tenant);

        var user = User.Create(
            tenantId: tenant.Id,
            email: email,
            passwordHash: ctx.PasswordHasher.Hash(plainPassword),
            fullName: "Test User",
            role: UserRole.SalonAdmin);

        if (!isActive) user.Archive();
        ctx.Db.Users.Add(user);
        ctx.Db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_success_and_marks_last_login()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        SeedUser(ctx, email: "ada@example.com", plainPassword: "Secret123");
        ctx.Clock.UtcNow = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

        var result = await handler.HandleAsync(
            new LoginCommand("ada@example.com", "Secret123", "127.0.0.1"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("ada@example.com");
        result.Value.Token.Should().StartWith("ACCESS-TOKEN-FOR-");
        result.Value.RefreshToken.Should().Be("refresh-plaintext-1");

        // MarkLogin debe haber escrito el clock actual en BD.
        // IgnoreQueryFilters porque el DbContext filtra por TenantId del
        // CurrentTenantService (que en tests devuelve Guid.Empty por default).
        var dbUser = ctx.Db.Users.IgnoreQueryFilters().Single();
        dbUser.LastLoginAt.Should().Be(ctx.Clock.UtcNow);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        SeedUser(ctx, plainPassword: "Correct123");

        var result = await handler.HandleAsync(
            new LoginCommand("test@example.com", "Wrong123", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ApplicationErrorType.Unauthorized);
        result.Error.Code.Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_with_nonexistent_email_returns_same_generic_error()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        // No seed — el user no existe

        var result = await handler.HandleAsync(
            new LoginCommand("nobody@example.com", "Any123", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.invalid_credentials");
        // El mismo código que "wrong password": no revelar enumeration.
    }

    [Fact]
    public async Task Login_with_inactive_user_returns_unauthorized()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        SeedUser(ctx, isActive: false);

        var result = await handler.HandleAsync(
            new LoginCommand("test@example.com", "Password123", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_with_inactive_tenant_returns_tenant_inactive_error()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        var tenant = Tenant.Create("Inactive Salon", "inactive-salon");
        tenant.Deactivate();
        SeedUser(ctx, tenant: tenant);

        var result = await handler.HandleAsync(
            new LoginCommand("test@example.com", "Password123", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.tenant_inactive");
    }

    [Fact]
    public async Task Login_persists_refresh_token_in_db()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        SeedUser(ctx);

        var result = await handler.HandleAsync(
            new LoginCommand("test@example.com", "Password123", "1.2.3.4"), default);

        result.IsSuccess.Should().BeTrue();
        // RefreshTokens no implementa ITenantEntity → no necesita IgnoreQueryFilters.
        var refreshInDb = ctx.Db.RefreshTokens.Single();
        refreshInDb.TokenHash.Should().Be(HandlerTestContext.HashOf("refresh-plaintext-1"));
        refreshInDb.RevokedAt.Should().BeNull();
        refreshInDb.CreatedByIp.Should().Be("1.2.3.4");
        refreshInDb.ExpiresAt.Should().Be(ctx.Clock.UtcNow.AddDays(ctx.JwtSettings.RefreshTokenDays));
    }
}
