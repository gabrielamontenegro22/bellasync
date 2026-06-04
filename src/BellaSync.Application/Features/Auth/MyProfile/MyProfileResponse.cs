namespace BellaSync.Application.Features.Auth.MyProfile;

/// <summary>
/// Snapshot del perfil propio del user logueado.
/// Lo consume la página /mi-cuenta para mostrarle al user sus datos
/// y permitirle editarlos. NO incluye PasswordHash ni info sensible.
///
/// Es similar a UserResponse pero agrega contexto del tenant
/// (nombre del salón) para que el header de la página lo muestre.
/// </summary>
public sealed class MyProfileResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    /// <summary>"SalonAdmin" | "Receptionist" | "Stylist" | "SuperAdmin".</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Nombre del salón al que pertenece. Null si es SuperAdmin
    /// (no pertenece a ningún tenant operativo).
    /// </summary>
    public string? TenantName { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
