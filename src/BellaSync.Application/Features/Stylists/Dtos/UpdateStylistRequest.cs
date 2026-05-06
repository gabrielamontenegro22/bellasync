namespace BellaSync.Application.Features.Stylists.Dtos;

/// <summary>
/// Request para editar un estilista existente.
/// Permite reactivar a un estilista archivado (IsActive=true).
/// La lista ServiceIds reemplaza COMPLETAMENTE las asignaciones actuales.
/// </summary>
public class UpdateStylistRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Reemplaza la lista de servicios asignados al estilista.
    /// - Lista vacía → se quitan todas las asignaciones existentes.
    /// - Lista con ids → se sincroniza el set: se agregan los nuevos y se quitan los que ya no estén.
    /// </summary>
    public List<Guid> ServiceIds { get; set; } = new();
}
