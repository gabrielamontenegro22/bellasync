using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Cliente del salón. Aislado por TenantId.
/// Soporta dos canales de creación:
///  - Recepcionista desde el panel (rol SalonAdmin / Receptionist)
///  - Cliente directo desde el portal público al agendar (futuro)
///
/// El borrado es lógico: IsActive=false. Las citas pasadas siguen
/// referenciándolo correctamente y el historial se preserva.
/// </summary>
public class Customer : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto. Único entre clientes ACTIVOS del salón.
    /// Es el identificador de hecho que usa la recepción para buscarlos.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>
    /// Fecha de nacimiento. Útil para promociones de cumpleaños.
    /// Solo guardamos día/mes/año (sin hora).
    /// </summary>
    public DateOnly? Birthday { get; set; }

    /// <summary>Documento de identidad (cédula). Opcional.</summary>
    public string? DocumentNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>
    /// Notas internas del salón sobre el cliente.
    /// Texto libre — preferencias, observaciones, comentarios del estilista.
    /// "No le gusta el agua muy caliente", "prefiere atención con Andrea", etc.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// El cliente aceptó recibir comunicaciones de marketing
    /// (recordatorios, promos, cumpleaños) por WhatsApp/email.
    /// Por defecto false — opt-in explícito.
    /// </summary>
    public bool AcceptsMarketing { get; set; } = false;

    public bool IsActive { get; set; } = true;
}
