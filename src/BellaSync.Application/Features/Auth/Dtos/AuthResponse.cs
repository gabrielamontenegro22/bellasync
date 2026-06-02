namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>
/// Respuesta estándar de los endpoints de autenticación.
/// Devuelve el JWT, su expiración y datos básicos del usuario y tenant.
/// </summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Refresh token plaintext. Vive en el cliente; se intercambia en
    /// /api/Auth/refresh por un nuevo access+refresh (rotación).
    /// El servidor solo guarda el SHA256 hash de este token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}
