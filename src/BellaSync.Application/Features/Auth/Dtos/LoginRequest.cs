namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>
/// Credenciales para autenticación de usuarios existentes.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
