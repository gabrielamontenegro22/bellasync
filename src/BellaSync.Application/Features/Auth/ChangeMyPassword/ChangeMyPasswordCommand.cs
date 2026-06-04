using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Auth.ChangeMyPassword;

/// <summary>
/// Cambio de contraseña iniciado por el propio user (estando logueado).
/// Diferente a ResetPassword (que usa un token de email).
/// </summary>
public sealed record ChangeMyPasswordCommand(
    string CurrentPassword,
    string NewPassword
) : ICommand;
