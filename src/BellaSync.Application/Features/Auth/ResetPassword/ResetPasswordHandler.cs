using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.ResetPassword;

/// <summary>
/// Cambia la contraseña usando un token de reset. Token es de un solo uso.
/// Revalida que el user y tenant sigan activos en el momento del uso.
/// Revoca todos los refresh tokens activos del user (cierra brecha #7c).
/// </summary>
public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        var entity = await _db.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
                .ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.Token == command.Token, ct);

        if (entity is null || entity.UsedAt.HasValue || entity.ExpiresAt < DateTime.UtcNow)
        {
            return ApplicationError.Validation(
                "auth.reset_token_invalid",
                "El enlace expiró o ya fue usado. Solicita uno nuevo.");
        }

        if (!entity.User.IsActive || entity.User.Tenant is { IsActive: false })
        {
            _logger.LogWarning(
                "Reset password rechazado por user/tenant inactivo: user={UserId} tenant={TenantId}",
                entity.User.Id, entity.User.TenantId);
            return ApplicationError.Validation(
                "auth.reset_token_invalid",
                "El enlace ya no es válido. Contacta al soporte si crees que es un error.");
        }

        entity.User.PasswordHash = _passwordHasher.Hash(command.NewPassword);
        entity.UsedAt = DateTime.UtcNow;

        // Revocar todos los refresh tokens activos del user.
        // Las sesiones existentes con access tokens válidos (15-30 min) seguirán
        // funcionando hasta expirar, pero ya no podrán refrescar.
        var activeRefreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == entity.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens) rt.Revoke();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completado para {Email}. Revocados {Count} refresh tokens.",
            entity.User.Email, activeRefreshTokens.Count);

        return Result.Success();
    }
}
