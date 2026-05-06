using System.Security.Claims;
using BellaSync.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Lee el claim 'tenant_id' del JWT del request en curso.
/// Si no hay HttpContext (ej. migraciones, jobs) o el claim no existe,
/// devuelve Guid.Empty y HasTenant = false.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    public const string TenantIdClaim = "tenant_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var raw = user?.FindFirst(TenantIdClaim)?.Value
                      ?? user?.FindFirst(ClaimTypes.GroupSid)?.Value;

            return Guid.TryParse(raw, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public bool HasTenant => TenantId != Guid.Empty;
}
