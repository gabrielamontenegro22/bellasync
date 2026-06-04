using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Users.Dtos;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Users.UpdateUser;

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, UserResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdateUserHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> HandleAsync(
        UpdateUserCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<UserRole>(command.Role, ignoreCase: true, out var newRole)
            || newRole == UserRole.SuperAdmin
            || newRole == UserRole.Stylist)
        {
            return ApplicationError.Validation(
                "user.invalid_role",
                "Rol inválido. Usá SalonAdmin o Receptionist.");
        }

        // El filtro multi-tenant evita tocar users de otros tenants.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (user is null)
            return ApplicationError.NotFound("user.not_found", "Usuario no encontrado.");

        // Guard: si estamos bajando un SalonAdmin a Receptionist, asegurarnos
        // de que NO sea el último SalonAdmin activo. Quedar sin admin tira
        // al salón a un estado sin recuperación desde el panel.
        var wasAdmin = user.Role == UserRole.SalonAdmin;
        var becomesNonAdmin = newRole != UserRole.SalonAdmin;
        if (wasAdmin && becomesNonAdmin)
        {
            var otherActiveAdmins = await _db.Users
                .CountAsync(u => u.Id != user.Id
                              && u.Role == UserRole.SalonAdmin
                              && u.IsActive, ct);
            if (otherActiveAdmins == 0)
                return ApplicationError.Conflict(
                    "user.last_admin",
                    "No podés cambiar el rol del último administrador del salón.");
        }

        try
        {
            user.Rename(command.FullName);
            if (user.Role != newRole) user.ChangeRole(newRole);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("user.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} actualizado en tenant {TenantId}",
            user.Id, _currentTenant.TenantId);

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
