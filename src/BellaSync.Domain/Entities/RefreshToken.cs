using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Refresh token de larga vida usado para obtener access tokens nuevos
/// sin re-loguear. Cada acceso del refresh token genera un nuevo refresh
/// (rotación) y revoca el actual.
///
/// Hash, no plaintext: guardamos SHA256(token) en BD. Si la BD se filtra,
/// el atacante no tiene los tokens. El plaintext solo viaja en la respuesta
/// HTTP (una vez) y vive en el cliente.
///
/// Family tracking: cuando rotamos, encadenamos via ReplacedByTokenHash.
/// Si alguien intenta usar un token revocado (token reuse), eso indica
/// posible robo → revocamos toda la cadena.
///
/// NO implementa ITenantEntity: pertenece a un User, no a un Tenant directo
/// (un SuperAdmin tendría refresh tokens también).
/// </summary>
public class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp = null,
        string? replacesTokenHash = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId es obligatorio.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("TokenHash es obligatorio.");

        var token = new RefreshToken();
        token.UserId = userId;
        token.TokenHash = tokenHash;
        token.ExpiresAt = expiresAtUtc;
        token.CreatedByIp = createdByIp;
        token.ReplacesTokenHash = replacesTokenHash;
        return token;
    }

    public Guid UserId { get; private set; }

    /// <summary>SHA256 del token plaintext. El plaintext nunca se persiste.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Null si nunca se revocó. Cuando se revoca (o rota), se setea.</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Si este token fue creado al rotar otro, guardamos el hash del original
    /// para reconstruir la cadena. Útil para auditoría e investigación de reuse.
    /// </summary>
    public string? ReplacesTokenHash { get; private set; }

    /// <summary>
    /// Si este token fue rotado y reemplazado por otro, guardamos el hash
    /// del que lo reemplazó. Permite detectar reuse: si alguien intenta usar
    /// un token con ReplacedByTokenHash != null, es porque el legítimo ya rotó.
    /// </summary>
    public string? ReplacedByTokenHash { get; private set; }

    /// <summary>IP desde la que se emitió el token. Opcional, para auditoría.</summary>
    public string? CreatedByIp { get; private set; }

    public User User { get; private set; } = null!;

    // ===== MÉTODOS VERBALES =====

    public bool IsActive() => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Marca el token como revocado. Si fue rotado por otro, guarda el hash
    /// del reemplazo para auditoría.
    /// </summary>
    public void Revoke(string? replacedByHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByHash;
    }
}
