namespace BellaSync.Application.Features.Users.Dtos;

/// <summary>
/// Snapshot de un usuario del salón. Lo que ve la admin en
/// Configuración → Usuarios. No exponemos PasswordHash ni nada
/// sensible.
/// </summary>
public sealed class UserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    /// <summary>"SalonAdmin" | "Receptionist" | "Stylist".</summary>
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
