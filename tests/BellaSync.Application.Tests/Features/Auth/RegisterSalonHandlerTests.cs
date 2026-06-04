using BellaSync.Application.Common.Errors;
using BellaSync.Application.Features.Auth.RegisterSalon;
using BellaSync.Application.Tests.Helpers;
using BellaSync.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
// Tests usan IgnoreQueryFilters() porque el DbContext aplica filtro
// global multi-tenant y CurrentTenantService devuelve Guid.Empty en tests.

namespace BellaSync.Application.Tests.Features.Auth;

public class RegisterSalonHandlerTests
{
    private static (HandlerTestContext, RegisterSalonHandler) Setup()
    {
        var ctx = new HandlerTestContext();
        var handler = new RegisterSalonHandler(
            ctx.Db, ctx.PasswordHasher, ctx.TokenIssuer, ctx.Clock, ctx.Logger<RegisterSalonHandler>());
        return (ctx, handler);
    }

    [Fact]
    public async Task RegisterSalon_creates_tenant_user_and_returns_tokens()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        var result = await handler.HandleAsync(
            new RegisterSalonCommand("Bella Spa", "Ada Lovelace", "ada@bella.com", "Secret123", null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TenantName.Should().Be("Bella Spa");
        result.Value.TenantSlug.Should().Be("bella-spa");
        result.Value.Email.Should().Be("ada@bella.com");
        result.Value.Role.Should().Be("SalonAdmin");
        result.Value.Token.Should().NotBeEmpty();
        result.Value.RefreshToken.Should().NotBeEmpty();

        var tenant = ctx.Db.Tenants.IgnoreQueryFilters().Single();
        tenant.Name.Should().Be("Bella Spa");
        tenant.IsActive.Should().BeTrue();

        var user = ctx.Db.Users.IgnoreQueryFilters().Single();
        user.TenantId.Should().Be(tenant.Id);
        user.Email.Should().Be("ada@bella.com");
        user.Role.Should().Be(UserRole.SalonAdmin);
    }

    [Fact]
    public async Task RegisterSalon_rejects_duplicate_email()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        // Pre-seed un user con el mismo email
        var existingTenant = Tenant.Create("Otro", "otro");
        ctx.Db.Tenants.Add(existingTenant);
        ctx.Db.Users.Add(User.Create(
            existingTenant.Id, "ada@bella.com", "HASH:old", "Otro", UserRole.SalonAdmin));
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new RegisterSalonCommand("Otro Salón", "Ada", "ada@bella.com", "Secret123", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ApplicationErrorType.Conflict);
        result.Error.Code.Should().Be("user.email_taken");
    }

    [Fact]
    public async Task RegisterSalon_suffixes_slug_when_collision()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        ctx.Db.Tenants.Add(Tenant.Create("Bella Spa", "bella-spa"));
        ctx.Db.SaveChanges();

        var result = await handler.HandleAsync(
            new RegisterSalonCommand("Bella Spa", "Ada", "another@bella.com", "Secret123", null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TenantSlug.Should().StartWith("bella-spa-")
            .And.NotBe("bella-spa");
    }

    [Fact]
    public async Task RegisterSalon_normalizes_email_to_lowercase()
    {
        var (ctx, handler) = Setup();
        using var _ = ctx;

        var result = await handler.HandleAsync(
            new RegisterSalonCommand("Spa", "Ada", "  ADA@BELLA.COM  ", "Secret123", null),
            default);

        result.IsSuccess.Should().BeTrue();
        var user = await ctx.Db.Users.IgnoreQueryFilters().SingleAsync();
        user.Email.Should().Be("ada@bella.com");
    }
}
