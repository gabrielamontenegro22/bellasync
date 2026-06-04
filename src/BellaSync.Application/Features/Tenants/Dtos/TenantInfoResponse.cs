namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Info pública/contacto del salón. Lo usa el form de Configuración →
/// Información general, y a futuro el portal público de booking
/// (vía un endpoint público dedicado).
/// </summary>
public class TenantInfoResponse
{
    /// <summary>Nombre del salón ("Bella Spa Neiva").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Slug del URL público ("bella-spa-neiva") — read-only por ahora.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ContactEmail { get; set; }
    public string? LogoUrl { get; set; }
    public string? InstagramHandle { get; set; }
    public string? Description { get; set; }
}
