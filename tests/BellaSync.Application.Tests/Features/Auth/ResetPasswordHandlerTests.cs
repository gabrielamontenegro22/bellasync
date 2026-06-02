using BellaSync.Application.Common.Errors;
using BellaSync.Application.Features.Auth.ResetPassword;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Tests.Features.Auth;

public class ResetPasswordHandlerTests
{
    private static (HandlerTestContext, ResetPasswordHandler, User) Setup()
    {
        var ctx = new HandlerTestContext();
        var handler = new ResetPasswordHandler(
            ctx.Db, ctx.PasswordHasher, ctx.Clock, ctx.Logger<ResetPasswordHandler>());

        var tenant = Tenant.Create("Test", "test");
        ctx.Db.Tenants.Add(tenant);
        var user = User.Create(tenant.Id, "u@e.com", "HASH:OldPassword", "User", UserRole.SalonAdmin);
        ctx.Db.Users.Add(user);
        ctx.Db.SaveChanges();
        return (ctx, handler, user);
    }

    [Fact]
    public async Task Reset_with_valid_token_changes_password_and_revokes_refresh_tokens()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        // Seedeo un token de reset válido
        var resetToken = PasswordResetToken.Create(user.Id, "tok-123", ctx.Clock.UtcNow.AddHours(1));
        ctx.Db.PasswordResetTokens.Add(resetToken);

        // Seedeo 2 refresh tokens activos del user (deben quedar revocados)
        ctx.Db.RefreshTokens.Add(RefreshToken.Create(
            user.Id, HandlerTestContext.HashOf("rt-1"), ctx.Clock.UtcNow.AddDays(10)));
        ctx.Db.RefreshTokens.Add(RefreshToken.Create(
            user.Id, HandlerTestContext.HashOf("rt-2"), ctx.Clock.UtcNow.AddDays(10)));
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("tok-123", "NewPassword456"), default);

        result.IsSuccess.Should().BeTrue();

        // Password cambió. IgnoreQueryFilters: User implementa ITenantEntity
        // y el DbContext filtra por TenantId del CurrentTenantService.
        var dbUser = ctx.Db.Users.IgnoreQueryFilters().Single();
        dbUser.PasswordHash.Should().Be("HASH:NewPassword456");

        // Token marcado como usado
        var dbToken = ctx.Db.PasswordResetTokens.Single();
        dbToken.UsedAt.Should().Be(ctx.Clock.UtcNow);

        // Los 2 refresh tokens revocados
        ctx.Db.RefreshTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Reset_with_expired_token_returns_validation_error()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        var resetToken = PasswordResetToken.Create(user.Id, "tok-exp", ctx.Clock.UtcNow.AddHours(-1));
        ctx.Db.PasswordResetTokens.Add(resetToken);
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("tok-exp", "NewPass123"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.reset_token_invalid");

        ctx.Db.Users.IgnoreQueryFilters().Single().PasswordHash.Should().Be("HASH:OldPassword");
    }

    [Fact]
    public async Task Reset_with_already_used_token_returns_validation_error()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        var resetToken = PasswordResetToken.Create(user.Id, "tok-used", ctx.Clock.UtcNow.AddHours(1));
        resetToken.MarkUsed(ctx.Clock.UtcNow);
        ctx.Db.PasswordResetTokens.Add(resetToken);
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("tok-used", "NewPass123"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.reset_token_invalid");
    }

    [Fact]
    public async Task Reset_with_unknown_token_returns_validation_error()
    {
        var (ctx, handler, _) = Setup();
        using var _ = ctx;

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("not-in-db", "NewPass123"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.reset_token_invalid");
    }

    [Fact]
    public async Task Reset_for_inactive_user_returns_validation_error()
    {
        var (ctx, handler, user) = Setup();
        using var _ = ctx;

        user.Archive();
        ctx.Db.SaveChanges();

        var resetToken = PasswordResetToken.Create(user.Id, "tok-x", ctx.Clock.UtcNow.AddHours(1));
        ctx.Db.PasswordResetTokens.Add(resetToken);
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("tok-x", "NewPass123"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.reset_token_invalid");
    }
}
