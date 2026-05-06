using System.Text;
using BellaSync.Application;
using BellaSync.Infrastructure;
using BellaSync.Infrastructure.Auth;
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

    // Controllers + JSON
    builder.Services.AddControllers();
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
