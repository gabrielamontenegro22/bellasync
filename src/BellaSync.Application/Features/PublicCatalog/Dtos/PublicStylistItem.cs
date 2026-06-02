namespace BellaSync.Application.Features.PublicCatalog.Dtos;

/// <summary>
/// Estilista visible para el cliente del portal público.
/// Incluye los IDs de servicios que sabe hacer (para filtrar quién atiende qué).
/// No expone email, teléfono, cédula ni cargo interno — info para uso de la recepción.
/// </summary>
public class PublicStylistItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;   // "Estilista", "Colorista", etc. (público OK)
    public string? Color { get; set; }
    public List<Guid> ServiceIds { get; set; } = new();
}
