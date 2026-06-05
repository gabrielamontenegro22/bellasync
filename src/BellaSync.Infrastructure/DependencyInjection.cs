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
        // Scoped: cachea snapshot de permisos por request (1 query por
        // request aunque varios handlers chequeen distintos permisos).
        services.AddScoped<IReceptionPermissionsService, ReceptionPermissionsService>();
        // Storage de archivos subidos (vouchers, logos). Local en dev/on-prem.
        // Swap por S3FileStorage cuando se haga deploy a cloud.
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Email: switch entre Logging (dev) y Resend (prod) según config.
        // Si Provider="Resend" pero falta ApiKey, caemos a Logging con un
        // warning para que dev no se rompa por config olvidada.
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        var emailSettings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>()
            ?? new EmailSettings();
        var useResend = string.Equals(emailSettings.Provider, "Resend", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(emailSettings.Resend?.ApiKey);

        if (useResend)
        {
            // HttpClient gestionado por IHttpClientFactory → reusa conexiones,
            // respeta DNS refresh, evita socket exhaustion en alto volumen.
            services.AddHttpClient<IEmailService, ResendEmailService>();
        }
        else
        {
            // Default dev: loguea con Serilog. Si Provider=Resend pero falta
            // ApiKey, cae silenciosamente acá — el logger del startup ya
            // imprime el provider seleccionado, y cada email que se "envía"
            // imprime un warning bien visible ("DEV MODE — no se envió email").
            services.AddScoped<IEmailService, LoggingEmailService>();
        }

        services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();
        // Configuración de pagos por salón — scoped para cachear durante el request.
        services.AddScoped<ITenantAppointmentSettings, TenantAppointmentSettingsService>();

        // Clock: stateless, una sola instancia para toda la app.
        // En tests se reemplaza por FakeClock controlado.
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
