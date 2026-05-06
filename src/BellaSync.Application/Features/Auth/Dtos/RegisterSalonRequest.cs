namespace BellaSync.Application.Features.Auth.Dtos;

/// <summary>
/// Datos requeridos para registrar un salón nuevo en el SaaS.
/// Crea Tenant + User admin en una sola operación atómica.
/// </summary>
public class RegisterSalonRequest
{
    /// <summary>Nombre comercial del salón (ej. "Bella Spa Neiva").</summary>
    public string SalonName { get; set; } = string.Empty;

    /// <summary>Nombre completo del usuario administrador del salón.</summary>
    public string AdminFullName { get; set; } = string.Empty;

    /// <summary>Email del usuario administrador (será su credencial de login).</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>Contraseña en texto plano. Se hashea antes de persistir.</summary>
    public string AdminPassword { get; set; } = string.Empty;
}
