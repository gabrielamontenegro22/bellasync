namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>Request del endpoint POST /api/Auth/refresh.</summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
