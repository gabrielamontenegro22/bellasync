using BellaSync.Application.Auth;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Auth.Shared;
using BellaSync.Domain.Entities;
using BellaSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BellaSync.Application.Tests.Helpers;

/// <summary>
/// Setup compartido para tests de handlers: DbContext InMemory + fakes
/// configurables. Cada test instancia uno nuevo (DbContext único por test).
///
/// Uso típico:
///   using var ctx = new HandlerTestContext();
///   ctx.CurrentTenant.TenantId.Returns(myTenantId);
///   var handler = new LoginHandler(ctx.Db, ctx.PasswordHasher, ctx.TokenIssuer, ctx.Clock);
/// </summary>
public sealed class HandlerTestContext : IDisposable
{
    public ApplicationDbContext Db { get; }
    public ICurrentTenantService CurrentTenant { get; }
    public ICurrentUserService CurrentUser { get; }
    public IPasswordHasher PasswordHasher { get; }
    public IJwtTokenService Jwt { get; }
    public IRefreshTokenGenerator RefreshTokens { get; }
    public FakeClock Clock { get; }
    public AuthTokenIssuer TokenIssuer { get; }
    public JwtSettings JwtSettings { get; }

    public HandlerTestContext()
    {
        CurrentTenant = Substitute.For<ICurrentTenantService>();
        CurrentTenant.TenantId.Returns(Guid.Empty);
        CurrentTenant.HasTenant.Returns(false);

        // User actual: por default uno admin con UserId fijo. Los tests
        // que quieran simular recepción o anónimo pueden sobreescribir
        // las propiedades del mock.
        CurrentUser = Substitute.For<ICurrentUserService>();
        CurrentUser.UserId.Returns(Guid.NewGuid());
        CurrentUser.Role.Returns("SalonAdmin");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"BellaSyncTest-{Guid.NewGuid()}")
            .UseSnakeCaseNamingConvention()
            // InMemory provider no soporta transacciones; los handlers no
            // las usan explícitamente (SaveChanges es atómico).
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new ApplicationDbContext(options, CurrentTenant);

        PasswordHasher = Substitute.For<IPasswordHasher>();
        // Por defecto: hash = "HASH:" + plain, verify si hash empieza con "HASH:" + plain
        PasswordHasher.Hash(Arg.Any<string>()).Returns(call => $"HASH:{call.Arg<string>()}");
        PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1) == $"HASH:{call.ArgAt<string>(0)}");

        Jwt = Substitute.For<IJwtTokenService>();
        Clock = new FakeClock();
        Jwt.GenerateToken(Arg.Any<User>()).Returns(call =>
            ("ACCESS-TOKEN-FOR-" + call.Arg<User>().Id, Clock.UtcNow.AddMinutes(30)));

        RefreshTokens = Substitute.For<IRefreshTokenGenerator>();
        var refreshCounter = 0;
        RefreshTokens.Generate().Returns(_ =>
        {
            refreshCounter++;
            var plaintext = $"refresh-plaintext-{refreshCounter}";
            return (plaintext, HashOf(plaintext));
        });
        RefreshTokens.Hash(Arg.Any<string>()).Returns(call => HashOf(call.Arg<string>()));

        JwtSettings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            Secret = new string('x', 64),
            ExpirationMinutes = 30,
            RefreshTokenDays = 30,
        };

        TokenIssuer = new AuthTokenIssuer(
            Db, Jwt, RefreshTokens, Clock,
            Options.Create(JwtSettings));
    }

    /// <summary>Hash predecible para los refresh tokens fake.</summary>
    public static string HashOf(string plaintext) => $"HASH({plaintext})";

    public NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    public void Dispose() => Db.Dispose();
}
