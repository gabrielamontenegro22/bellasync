using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Stylists.Dtos;

/// <summary>
/// Request para editar un estilista existente.
/// Permite cambiar el estado (Active/Vacation/Inactive).
/// La lista ServiceIds reemplaza COMPLETAMENTE las asignaciones actuales.
/// </summary>
public class UpdateStylistRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = "Estilista";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? IdNumber { get; set; }

    public string? Color { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Reemplaza al antiguo IsActive (bool).
    /// Soporta Active / Vacation / Inactive.
    /// </summary>
    public StylistStatus Status { get; set; } = StylistStatus.Active;

    /// <summary>
    /// Reemplaza la lista de servicios asignados al estilista.
    /// </summary>
    public List<Guid> ServiceIds { get; set; } = new();
}
