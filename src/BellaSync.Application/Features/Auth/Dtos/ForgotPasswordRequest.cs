namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>Solicitud para enviar enlace de reseteo de contraseña por email.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
