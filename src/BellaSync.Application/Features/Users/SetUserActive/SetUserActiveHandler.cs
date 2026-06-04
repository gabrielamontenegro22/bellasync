using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Users.SetUserActive;

public sealed class SetUserActiveHandler : ICommandHandler<SetUserActiveCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetUserActiveHandler> _logger;

    public SetUserActiveHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<SetUserActiveHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SetUserActiveCommand command, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (user is null)
            return ApplicationError.NotFound("user.not_found", "Usuario no encontrado.");

        // Idempotente.
        if (user.IsActive == command.IsActive) return Result.Success();

        // Guards al archivar:
        // 1. No archivar al último SalonAdmin activo (dejaría al salón sin admin)
        // 2. No archivarse a sí mismo (la admin se quedaría sin acceso al instante)
        if (!command.IsActive)
        {
            if (_currentUser.UserId == user.Id)
                return ApplicationError.Conflict(
                    "user.self_archive",
                    "No podés archivar tu propio usuario.");

            if (user.Role == UserRole.SalonAdmin)
            {
                var otherActiveAdmins = await _db.Users
                    .CountAsync(u => u.Id != user.Id
                                  && u.Role == UserRole.SalonAdmin
                                  && u.IsActive, ct);
                if (otherActiveAdmins == 0)
                    return ApplicationError.Conflict(
                        "user.last_admin",
                        "No podés archivar al último administrador del salón.");
            }
        }

        if (command.IsActive) user.Reactivate(); else user.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} {Action}",
            user.Id, command.IsActive ? "reactivado" : "archivado");

        return Result.Success();
    }
}
