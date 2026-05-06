using BellaSync.Domain.Entities;

namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Genera tokens JWT firmados que incluyen los claims necesarios
/// para autenticar y autorizar (sub, email, role, tenant_id).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Genera un JWT para el usuario indicado.
    /// </summary>
    /// <param name="user">Usuario autenticado.</param>
    /// <returns>Token JWT firmado y la fecha de expiración (UTC).</returns>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
