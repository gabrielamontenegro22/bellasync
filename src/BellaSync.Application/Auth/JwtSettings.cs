namespace BellaSync.Application.Auth;

/// <summary>
/// Sección "Jwt" en appsettings.json — bind con IOptions&lt;JwtSettings&gt;.
///
/// Vive en Application (no Infrastructure) porque define política de auth
/// que tanto los handlers (en Application) como JwtTokenService (en
/// Infrastructure) necesitan consultar.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// TTL del access token (JWT). Corto a propósito (15-30 min): la idea
    /// es que el cliente lo refresque vía /api/Auth/refresh con el
    /// refresh token cuando expire.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// TTL del refresh token (días). Largo (30 días) para que el usuario
    /// no tenga que re-loguearse. La rotación en cada uso mitiga el riesgo
    /// de robo: un token robado vale, como mucho, hasta el próximo refresh
    /// legítimo del usuario real.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;
}
