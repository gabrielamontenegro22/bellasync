using BellaSync.Application.Auth;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Infrastructure.Persistence;
using BellaSync.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BellaSync.Infrastructure;

/// <summary>
/// Registro de servicios de Infrastructure en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuración tipada de JWT
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // EF Core con PostgreSQL
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no está configurada.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            })
            // Naming convention snake_case: tablas y columnas idiomáticas
            // de PostgreSQL ("created_at" en vez de "CreatedAt"). Cualquier
            // SQL crudo desde pgAdmin/psql es legible sin necesidad de quotes.
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // Servicios de aplicación
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        // Storage de archivos subidos (vouchers, logos). Local en dev/on-prem.
        // Swap por S3FileStorage cuando se haga deploy a cloud.
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, LoggingEmailService>();
        services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();
        // Configuración de pagos por salón — scoped para cachear durante el request.
        services.AddScoped<ITenantAppointmentSettings, TenantAppointmentSettingsService>();

        // Clock: stateless, una sola instancia para toda la app.
        // En tests se reemplaza por FakeClock controlado.
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
