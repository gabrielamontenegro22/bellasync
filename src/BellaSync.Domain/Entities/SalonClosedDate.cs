using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Día específico en que el salón está cerrado (vacaciones del equipo,
/// fiesta privada del salón, festivo local no nacional, etc.).
///
/// Único por (tenant_id, closed_date) — un salón no puede tener dos
/// rows para la misma fecha. PG 23505 → 409 Conflict.
/// </summary>
public class SalonClosedDate : BaseEntity, ITenantEntity
{
    private SalonClosedDate() { }

    /// <summary>
    /// Factory: crea un cierre puntual. Validación adicional acá:
    ///   - ClosedDate no puede ser pasada (no tiene sentido cerrar ayer).
    ///   - Reason es opcional, max 200 chars.
    /// </summary>
    public static SalonClosedDate Create(
        Guid tenantId,
        DateOnly closedDate,
        DateOnly todayColombia,
        string? reason = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (closedDate < todayColombia)
            throw new DomainException("No se puede agregar un cierre en una fecha pasada.");

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason is not null && normalizedReason.Length > 200)
            throw new DomainException("La razón no puede pasar de 200 caracteres.");

        return new SalonClosedDate
        {
            TenantId = tenantId,
            ClosedDate = closedDate,
            Reason = normalizedReason,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>Fecha cerrada (zona Colombia).</summary>
    public DateOnly ClosedDate { get; private set; }

    /// <summary>Razón opcional ("Día del estilista", "Cierre por evento", etc.).</summary>
    public string? Reason { get; private set; }
}
