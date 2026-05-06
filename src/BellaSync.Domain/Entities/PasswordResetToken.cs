using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Token de un solo uso para restablecer contraseña.
/// NO implementa ITenantEntity porque el flujo es anónimo
/// (el usuario perdió su sesión, no hay JWT en el request).
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Token hex de 64 caracteres (32 bytes random).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Vence 1 hora después de generarse (configurable).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Null si aún no se usó. Cuando se usa, se setea a UtcNow.</summary>
    public DateTime? UsedAt { get; set; }

    // Navegación
    public User User { get; set; } = null!;
}
