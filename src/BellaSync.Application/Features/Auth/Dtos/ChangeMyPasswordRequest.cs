namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>
/// Body de POST /api/Auth/change-password. El user actual cambia su propia
/// contraseña verificando la actual.
/// </summary>
public sealed class ChangeMyPasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
