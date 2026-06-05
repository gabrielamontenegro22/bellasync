using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.Shared;
using BellaSync.Application.Features.Subscription;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.RegisterSalon;

public sealed class RegisterSalonHandler : ICommandHandler<RegisterSalonCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly IClock _clock;
    private readonly ILogger<RegisterSalonHandler> _logger;

    public RegisterSalonHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        AuthTokenIssuer tokenIssuer,
        IClock clock,
        ILogger<RegisterSalonHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RegisterSalonCommand command, CancellationToken ct)
    {
        var normalizedEmail = command.AdminEmail.Trim().ToLowerInvariant();
        var slug = SlugGenerator.Generate(command.SalonName);

        var slugExists = await _db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, ct);
        if (slugExists)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
        {
            return ApplicationError.Conflict(
                "user.email_taken",
                "Ya existe un usuario con ese correo electrónico.");
        }

        // Factories del dominio: validan invariantes (nombres no vacíos,
        // emails normalizados, etc.) con setters privados protegidos.
        var tenant = Tenant.Create(name: command.SalonName, slug: slug);

        var adminUser = User.Create(
            tenantId: tenant.Id,
            email: normalizedEmail,
            passwordHash: _passwordHasher.Hash(command.AdminPassword),
            fullName: command.AdminFullName,
            role: UserRole.SalonAdmin);

        // Suscripción inicial: arranca en trial profesional. Esto evita
        // que el GetSubscriptionHandler tenga que crear la sub on-demand
        // (anti-patrón: query mutando estado).
        var subscription = TenantSubscription.StartTrial(
            tenantId: tenant.Id,
            planCode: SubscriptionPlanCatalog.DefaultPlanCode,
            trialDays: SubscriptionPlanCatalog.DefaultTrialDays,
            utcNow: _clock.UtcNow);

        // Seed de 5 categorías default de inventario. Para que la admin
        // vea opciones apenas entra a /inventario sin tener que crearlas
        // desde cero. Puede borrarlas/renombrarlas libremente después.
        // Espeja exactamente el backfill SQL de la migración AddCustomProductCategories.
        var defaultCategories = new[]
        {
            ProductCategory.Create(tenant.Id, "Cabello",    ProductTone.Amber),
            ProductCategory.Create(tenant.Id, "Uñas",       ProductTone.Rose),
            ProductCategory.Create(tenant.Id, "Depilación", ProductTone.Sand),
            ProductCategory.Create(tenant.Id, "Spa",        ProductTone.Wine),
            ProductCategory.Create(tenant.Id, "Accesorios", ProductTone.Mist),
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(adminUser);
        _db.TenantSubscriptions.Add(subscription);
        foreach (var cat in defaultCategories) _db.ProductCategories.Add(cat);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Salón {SalonName} ({TenantId}) registrado con admin {Email}",
            tenant.Name, tenant.Id, adminUser.Email);

        var response = await _tokenIssuer.IssueAsync(
            user: adminUser,
            tenant: tenant,
            replacesTokenHash: null,
            createdByIp: command.CreatedByIp,
            ct: ct);

        return Result<AuthResponse>.Success(response);
    }
}
