using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Users.Dtos;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Users.CreateUser;

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, UserResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IPasswordHasher hasher,
        ILogger<CreateUserHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> HandleAsync(
        CreateUserCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized("user.no_tenant", "Sesión inválida.");

        // Validaciones básicas (lo demás lo valida User.Create).
        if (string.IsNullOrWhiteSpace(command.Email))
            return ApplicationError.Validation("user.email_required", "El email es obligatorio.");
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 6)
            return ApplicationError.Validation(
                "user.password_too_short",
                "La contraseña debe tener al menos 6 caracteres.");

        // Solo permitimos crear SalonAdmin y Receptionist desde el panel
        // del salón. SuperAdmin se crea via bootstrap; Stylist es entity
        // separada (Stylist), no User — la persona que atiende clientes
        // no necesariamente loguea al sistema.
        if (!Enum.TryParse<UserRole>(command.Role, ignoreCase: true, out var role)
            || role == UserRole.SuperAdmin
            || role == UserRole.Stylist)
        {
            return ApplicationError.Validation(
                "user.invalid_role",
                "Rol inválido. Usá SalonAdmin o Receptionist.");
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // Email único global (es el identificador de login). Si ya existe,
        // 409 — no importa si es de otro tenant o del mismo.
        var emailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
            return ApplicationError.Conflict(
                "user.email_taken",
                "Ya existe un usuario con ese email.");

        User user;
        try
        {
            user = User.Create(
                tenantId: _currentTenant.TenantId,
                email: normalizedEmail,
                passwordHash: _hasher.Hash(command.Password),
                fullName: command.FullName,
                role: role);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("user.invalid", ex.Message);
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} ({Role}) creado en tenant {TenantId}",
            user.Id, user.Role, _currentTenant.TenantId);

        return Result<UserResponse>.Success(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        });
    }
}
