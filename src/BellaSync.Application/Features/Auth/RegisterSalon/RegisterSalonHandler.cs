using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.RegisterSalon;

public sealed class RegisterSalonHandler : ICommandHandler<RegisterSalonCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly ILogger<RegisterSalonHandler> _logger;

    public RegisterSalonHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        AuthTokenIssuer tokenIssuer,
        ILogger<RegisterSalonHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RegisterSalonCommand command, CancellationToken ct)
    {
        var normalizedEmail = command.AdminEmail.Trim().ToLowerInvariant();
        var slug = SlugGenerator.Generate(command.SalonName);

        // Si el slug colisiona, le añadimos un sufijo aleatorio corto.
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

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = command.SalonName.Trim(),
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(command.AdminPassword),
            FullName = command.AdminFullName.Trim(),
            Role = UserRole.SalonAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(adminUser);
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
