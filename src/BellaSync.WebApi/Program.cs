using System.Text;
using BellaSync.Application;
using BellaSync.Infrastructure;
using BellaSync.Application.Auth;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Bootstrap logger para capturar errores de arranque
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Arrancando BellaSync API...");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog leído desde configuración
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext();
    });

    // Capas Application e Infrastructure
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Background job: libera holds vencidos cada 5 min. Sin esto las
    // citas Pending que el cliente nunca pagó quedan ocupando el cupo
    // del estilista para siempre. Ver ExpiredHoldsReleaseService.
    builder.Services.AddHostedService<BellaSync.WebApi.HostedServices.ExpiredHoldsReleaseService>();

    // WhatsApp: el sender es swappable (NoOp por default, después Twilio/Meta
    // sin tocar handlers). El dispatcher background corre cada 2min y se
    // encarga de encolar recordatorios + despacharlos.
    builder.Services.AddScoped<
        BellaSync.Application.Common.Interfaces.IWhatsAppSender,
        BellaSync.Application.Common.Services.NoOpWhatsAppSender>();
    builder.Services.AddHostedService<BellaSync.WebApi.HostedServices.WhatsAppDispatcherService>();

    // Suscripción SaaS: corre cada hora — emite facturas mensuales,
    // marca trials vencidos como PastDue, emite facturas tempranas
    // (7d antes del fin del período). Sin esto, las subs vencidas
    // se quedan "Active" eternamente sin facturar.
    builder.Services.AddHostedService<BellaSync.WebApi.HostedServices.SubscriptionDispatcherService>();

    // Política de citas (hold, anticipación mínima, etc.)
    builder.Services.Configure<BellaSync.Application.Auth.AppointmentSettings>(
        builder.Configuration.GetSection(BellaSync.Application.Auth.AppointmentSettings.SectionName));

    // Token compartido para endpoints internos (cron de release-expired-holds, etc.)
    builder.Services.Configure<BellaSync.WebApi.Controllers.InternalSettings>(
        builder.Configuration.GetSection(BellaSync.WebApi.Controllers.InternalSettings.SectionName));

    // Handler global para violaciones de constraint único (PG SQLSTATE 23505).
    // Traduce race conditions a 409 Conflict en vez de 500 Internal Server Error.
    // Vive en WebApi porque depende de tipos HTTP.
    builder.Services.AddExceptionHandler<UniqueViolationExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Controllers + JSON. JsonStringEnumConverter permite que el cliente
    // envíe los enums como string (ej. "Cabello") además de como número (0).
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();

    // CORS para el frontend (Vite/CRA en desarrollo)
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5173" };
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials());
    });

    // Autenticación JWT
    var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
    var jwt = jwtSection.Get<JwtSettings>()
        ?? throw new InvalidOperationException("Sección 'Jwt' no encontrada en configuración.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Dev local
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

    builder.Services.AddAuthorization();

    // Swagger con soporte para JWT
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "BellaSync API",
            Version = "v1",
            Description = "API del SaaS BellaSync — gestión integral para salones de belleza."
        });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Pega el token JWT (sin la palabra 'Bearer ').",
            Reference = new OpenApiReference
            {
                Id = JwtBearerDefaults.AuthenticationScheme,
                Type = ReferenceType.SecurityScheme
            }
        };

        c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { bearerScheme, Array.Empty<string>() }
        });
    });

    var app = builder.Build();

    // Pipeline
    // ExceptionHandler PRIMERO: convierte unique violations (PG 23505) que
    // escaparon a 409 Conflict en lugar de 500. Registrado en AddInfrastructure().
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BellaSync API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseSerilogRequestLogging();

    // En desarrollo no forzamos HTTPS para evitar el redirect en Swagger.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("BellaSync API lista en {Env}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BellaSync API falló al arrancar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Para tests de integración con WebApplicationFactory en el futuro
public partial class Program { }
