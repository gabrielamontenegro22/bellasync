using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Período en el que un estilista NO está disponible — vacaciones, día
/// libre, capacitación, licencia, cualquier cosa que requiera bloquear
/// agenda sin borrar al estilista del sistema.
///
/// Rango cerrado-cerrado: [FromDate, ToDate] inclusive ambos. Si Camila
/// se va del 15 al 22, los días 15 y 22 también están bloqueados.
///
/// Notas de diseño:
///   - Granularidad día completo (no horas). Si en el futuro hace falta
///     "medio día por cita médica", se modela como una cita interna en
///     la agenda, no acá.
///   - Una sola fila por rango (no se desnormaliza a un row por día) —
///     más legible para el usuario y mejor para reportes.
///   - Solapamientos entre rangos del MISMO estilista permitidos: la
///     query de "está disponible?" hace OR de todos los rangos. No
///     vale la pena la complejidad de validar y mergear.
///   - Múltiples estilistas pueden tener rangos solapados (todos
///     pueden estar de vacaciones en feriado, por ej.).
/// </summary>
public class StylistTimeOff : BaseEntity, ITenantEntity
{
    private StylistTimeOff() { }

    /// <summary>
    /// Factory: crea un período de vacaciones / día libre.
    ///   - FromDate &lt;= ToDate (mismo día permitido para 1 día libre).
    ///   - ToDate no puede ser pasada (no tiene sentido marcar ayer).
    ///   - Reason opcional, max 200 chars.
    /// </summary>
    public static StylistTimeOff Create(
        Guid tenantId,
        Guid stylistId,
        DateOnly fromDate,
        DateOnly toDate,
        DateOnly todayColombia,
        string? reason = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (stylistId == Guid.Empty)
            throw new DomainException("StylistId es obligatorio.");
        if (fromDate > toDate)
            throw new DomainException("La fecha 'desde' no puede ser posterior a 'hasta'.");
        if (toDate < todayColombia)
            throw new DomainException("No se puede marcar un período en fechas pasadas.");

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason is not null && normalizedReason.Length > 200)
            throw new DomainException("La razón no puede pasar de 200 caracteres.");

        return new StylistTimeOff
        {
            TenantId = tenantId,
            StylistId = stylistId,
            FromDate = fromDate,
            ToDate = toDate,
            Reason = normalizedReason,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>FK al estilista que no estará disponible.</summary>
    public Guid StylistId { get; private set; }

    /// <summary>Primer día del bloqueo (inclusive, hora Colombia).</summary>
    public DateOnly FromDate { get; private set; }

    /// <summary>Último día del bloqueo (inclusive, hora Colombia).</summary>
    public DateOnly ToDate { get; private set; }

    /// <summary>Razón opcional ("Vacaciones", "Capacitación", "Cita médica").</summary>
    public string? Reason { get; private set; }

    // Navegación inversa al estilista (opcional, útil para queries).
    public Stylist? Stylist { get; private set; }

    /// <summary>True si la fecha dada cae dentro del rango (cerrado-cerrado).</summary>
    public bool Includes(DateOnly date) => date >= FromDate && date <= ToDate;
}
