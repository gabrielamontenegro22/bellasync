using System.Security.Claims;
using BellaSync.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Lee el claim 'sub' (NameIdentifier) del JWT del request actual.
/// Devuelve null si no hay HttpContext o el claim no es parseable.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            // El JWT emitido por AuthTokenIssuer pone el rol en ClaimTypes.Role.
            return user?.FindFirst(ClaimTypes.Role)?.Value;
        }
    }
}
