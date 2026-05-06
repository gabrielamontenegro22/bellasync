namespace BellaSync.Infrastructure.Auth;

/// <summary>
/// Sección "Jwt" en appsettings.json — bind con IOptions&lt;JwtSettings&gt;.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
