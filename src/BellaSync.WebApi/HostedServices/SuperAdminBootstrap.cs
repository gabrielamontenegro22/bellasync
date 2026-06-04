using BellaSync.Application.Common.Interfaces;
using BellaSync.Domain.Entities;
using BellaSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.HostedServices;

/// <summary>
/// Asegura que exista un User con role SuperAdmin al arrancar la API.
/// Lee credenciales de appsettings "SuperAdmin" — en prod las setea
/// el operador del SaaS por env vars / secret manager.
///
/// Idempotente: si ya existe un user con ese email, no hace nada.
/// Si la sección "SuperAdmin" no está configurada, loguea warning y
/// salta (no rompe el bootstrap por falta de seed).
///
/// El SuperAdmin tiene TenantId = Guid.Empty (no pertenece a ningún
/// salón) y se loguea con el endpoint normal /api/Auth/login.
/// </summary>
public sealed class SuperAdminBootstrap : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<SuperAdminBootstrap> _logger;

    public SuperAdminBootstrap(
        IServiceProvider services,
        IConfiguration config,
        ILogger<SuperAdminBootstrap> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Bootstrap envuelto en try/catch: nunca queremos que un fallo del
        // seed tire la API abajo. Loggeamos y seguimos — la app arranca
        // sin SuperAdmin (el SuperAdmin podrá crearse manualmente más
        // tarde si se requiere).
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SuperAdminBootstrap falló — la API arranca igual sin SuperAdmin. " +
                "Verificar la BD manualmente.");
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var section = _config.GetSection("SuperAdmin");
        var email = section["Email"];
        var password = section["Password"];
        var fullName = section["FullName"] ?? "SaaS Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "SuperAdmin no configurado en appsettings — saltando seed.");
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rawDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Paso 1: asegurar que existe un Tenant "system" con Id=Empty.
        // Esto satisface la FK users.tenant_id → tenants.id (NOT NULL)
        // cuando creamos SuperAdmins que no pertenecen a ningún salón.
        //
        // Usamos SQL crudo en vez de EF porque BaseEntity.Id tiene un
        // initializer = Guid.NewGuid() que EF respeta — y sobreescribir
        // a Guid.Empty puede dispararle "value not set" en algunos
        // value generators. SQL directo evita todo eso y es más explícito.
        // Limpieza defensiva en 2 pasos: bootstraps viejos podían crear
        // el system tenant con Id aleatorio (BaseEntity initializer pisaba
        // el override Guid.Empty).
        //
        // Paso 1a: re-asignar cualquier SuperAdmin user que apunte a un
        //          _system orphan a Guid.Empty (donde estará la fila buena).
        // Paso 1b: borrar los _system orphans (ya sin FK violations).
        //
        // Sin el paso 1a, el DELETE viola fk_users_tenants_tenant_id y
        // la API queda fuera de servicio en cualquier entorno con data
        // legacy. Era exactamente el bug que descubrió el audit (C2).
        await rawDb.Database.ExecuteSqlRawAsync(
            @"UPDATE users
              SET tenant_id = '00000000-0000-0000-0000-000000000000'
              WHERE tenant_id IN (
                  SELECT id FROM tenants
                  WHERE slug = '_system'
                    AND id <> '00000000-0000-0000-0000-000000000000'
              );",
            cancellationToken);

        await rawDb.Database.ExecuteSqlRawAsync(
            @"DELETE FROM tenants
              WHERE slug = '_system'
                AND id <> '00000000-0000-0000-0000-000000000000';",
            cancellationToken);

        var rowCount = await rawDb.Database.ExecuteSqlRawAsync(
            @"INSERT INTO tenants (id, name, slug, is_active, created_at,
                hold_duration_hours, hold_min_before_appointment_minutes,
                min_advance_minutes, commissions_enabled, is_holidays_closed,
                lunch_break_enabled, lunch_break_from_hour, lunch_break_to_hour)
              VALUES ('00000000-0000-0000-0000-000000000000', 'BellaSync SaaS (System)',
                      '_system', true, NOW(), 24, 60, 60, false, false, false, 12, 13)
              ON CONFLICT (id) DO NOTHING;",
            cancellationToken);

        if (rowCount > 0)
            _logger.LogInformation("System tenant creado para SuperAdmins.");

        // Paso 2: crear el user SuperAdmin si no existe.
        var normalized = email.Trim().ToLowerInvariant();
        var exists = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalized, cancellationToken);

        if (exists)
        {
            _logger.LogInformation("SuperAdmin {Email} ya existe.", normalized);
            return;
        }

        var user = User.Create(
            tenantId: Guid.Empty,
            email: normalized,
            passwordHash: hasher.Hash(password),
            fullName: fullName,
            role: UserRole.SuperAdmin);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SuperAdmin {Email} creado en el bootstrap.", normalized);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
