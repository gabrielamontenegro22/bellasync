using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.ChangeMyPassword;

/// <summary>
/// Cambia el password del user autenticado. Valida la password actual
/// antes de aceptar el cambio (defensa contra session hijacking + impide
/// que alguien con acceso temporal a un equipo cambie la password sin
/// conocer la actual).
///
/// Side effect crítico: revoca TODOS los refresh tokens del user. La
/// sesión actual sigue viva hasta que expire el access token (~15 min),
/// luego el frontend va a /refresh, falla, y fuerza re-login con la
/// password nueva. Esto echa a cualquier intruso de otras sesiones.
/// </summary>
public sealed class ChangeMyPasswordHandler : ICommandHandler<ChangeMyPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly ILogger<ChangeMyPasswordHandler> _logger;

    public ChangeMyPasswordHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILogger<ChangeMyPasswordHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ChangeMyPasswordCommand command, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return ApplicationError.Unauthorized(
                "auth.not_authenticated",
                "No hay sesión activa.");

        // IgnoreQueryFilters: SuperAdmin tiene TenantId vacío y el filtro
        // multi-tenant lo dejaría afuera. Filtramos por UserId del JWT.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);

        if (user is null)
            return ApplicationError.NotFound("user.not_found", "Usuario no encontrado.");

        if (!user.IsActive)
            return ApplicationError.Forbidden(
                "user.inactive",
                "Tu cuenta está desactivada. Contactá al administrador del salón.");

        // Verificación del password actual: sin esto, alguien que se siente
        // un minuto frente a una sesión abierta puede cambiar la password.
        if (!_passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
            return ApplicationError.Validation(
                "auth.current_password_incorrect",
                "La contraseña actual no es correcta.");

        // No permitir setear exactamente la misma password (UX: evita confusión
        // del estilo "cambié la pass y no veo diferencia").
        if (_passwordHasher.Verify(command.NewPassword, user.PasswordHash))
            return ApplicationError.Validation(
                "auth.new_password_equals_current",
                "La contraseña nueva debe ser distinta a la actual.");

        user.ChangePassword(_passwordHasher.Hash(command.NewPassword));

        // Revocar todos los refresh tokens activos: cierra sesiones en otros
        // dispositivos. El device actual sigue funcionando hasta que el
        // access token expire (~15 min) — UX aceptable.
        var now = _clock.UtcNow;
        var activeRefreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens) rt.Revoke(now);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password cambiado por user propio {UserId}. {Count} refresh tokens revocados.",
            user.Id, activeRefreshTokens.Count);

        return Result.Success();
    }
}
