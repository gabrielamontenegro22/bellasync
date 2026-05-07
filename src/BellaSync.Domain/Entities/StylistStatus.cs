namespace BellaSync.Domain.Entities;

/// <summary>
/// Estado del estilista dentro del salón.
/// Reemplaza el antiguo IsActive (bool) para soportar también el estado "Vacaciones",
/// donde la persona sigue siendo parte del equipo pero no toma citas temporalmente.
/// </summary>
public enum StylistStatus
{
    /// <summary>Toma citas normalmente.</summary>
    Active = 0,

    /// <summary>Sigue en el equipo pero temporalmente no toma citas.</summary>
    Vacation = 1,

    /// <summary>
    /// Ya no forma parte del equipo (soft delete). No aparece en listas
    /// para agendar pero las citas históricas siguen referenciándolo.
    /// </summary>
    Inactive = 2,
}
