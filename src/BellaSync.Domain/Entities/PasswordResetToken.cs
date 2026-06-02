using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Token de un solo uso para restablecer contraseña.
/// NO implementa ITenantEntity porque el flujo es anónimo
/// (el usuario perdió su sesión, no hay JWT en el request).
///
/// Setters privados: solo se muta vía MarkUsed.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    private PasswordResetToken() { }

    /// <summary>
    /// Factory: crea un token nuevo. El token plaintext se genera fuera
    /// con un RNG criptográfico (típicamente en el handler).
    /// </summary>
    public static PasswordResetToken Create(
        Guid userId,
        string token,
        DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId es obligatorio.");
        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("Token es obligatorio.");

        var entity = new PasswordResetToken();
        entity.UserId = userId;
        entity.Token = token;
        entity.ExpiresAt = expiresAtUtc;
        return entity;
    }

    public Guid UserId { get; private set; }

    /// <summary>Token hex de 64 caracteres (32 bytes random).</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Vence 1 hora después de generarse (configurable).</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Null si aún no se usó. Cuando se usa, se setea a UtcNow.</summary>
    public DateTime? UsedAt { get; private set; }

    // Navegación
    public User User { get; private set; } = null!;

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Marca el token como usado. Idempotente.
    /// Si ya estaba usado, no hace nada (preserva la fecha original del primer uso).
    /// </summary>
    public void MarkUsed(DateTime utcNow)
    {
        if (UsedAt is null) UsedAt = utcNow;
    }

    /// <summary>True si el token no fue usado y no expiró.</summary>
    public bool IsActive(DateTime utcNow) => UsedAt is null && ExpiresAt > utcNow;
}
