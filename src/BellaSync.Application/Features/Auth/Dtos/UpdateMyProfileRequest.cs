namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>
/// Body de PUT /api/Auth/me. Por ahora solo nombre completo.
/// </summary>
public sealed class UpdateMyProfileRequest
{
    public string FullName { get; set; } = string.Empty;
}
