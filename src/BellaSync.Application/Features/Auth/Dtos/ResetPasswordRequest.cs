namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>Solicitud para guardar la nueva contraseña usando un token recibido por email.</summary>
public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
