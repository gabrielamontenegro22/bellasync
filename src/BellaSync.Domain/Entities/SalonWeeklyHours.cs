using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Horario semanal del salón: una fila por día de la semana que está
/// abierto, con hora de inicio y fin. La ausencia de una fila para un
/// día significa que ese día está cerrado.
///
/// Por qué tabla aparte y no 14 columnas en Tenant: queryable
/// ("¿qué salones abren los domingos?"), evita explosión de columnas
/// nullables, y deja espacio para una v2 con franjas múltiples
/// (mañana/tarde) o pausas, sin nueva migración del Tenant.
///
/// DayOfWeek = 0 (Lunes) … 6 (Domingo). Usamos esta convención
/// porque es la natural en Colombia ("la semana arranca lunes");
/// el enum System.DayOfWeek de .NET parte de Sunday=0, pero nosotros
/// lo mapeamos en el handler.
/// </summary>
public class SalonWeeklyHours : BaseEntity, ITenantEntity
{
    private SalonWeeklyHours() { }

    /// <summary>
    /// Factory: crea una fila de horario para un día. Valida invariantes:
    ///   - DayOfWeek ∈ [0, 6]
    ///   - FromHour ∈ [0, 24], ToHour ∈ [0, 24]
    ///   - FromHour &lt; ToHour (al menos 1h)
    /// </summary>
    public static SalonWeeklyHours Create(
        Guid tenantId,
        int dayOfWeek,
        int fromHour,
        int toHour)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new DomainException("DayOfWeek debe estar entre 0 (Lun) y 6 (Dom).");
        if (fromHour < 0 || fromHour > 24)
            throw new DomainException("FromHour debe estar entre 0 y 24.");
        if (toHour < 0 || toHour > 24)
            throw new DomainException("ToHour debe estar entre 0 y 24.");
        if (fromHour >= toHour)
            throw new DomainException("FromHour debe ser anterior a ToHour (al menos 1 hora).");

        return new SalonWeeklyHours
        {
            TenantId = tenantId,
            DayOfWeek = dayOfWeek,
            FromHour = fromHour,
            ToHour = toHour,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>0 = Lunes, 1 = Martes, …, 6 = Domingo.</summary>
    public int DayOfWeek { get; private set; }

    /// <summary>Hora de apertura (0-24).</summary>
    public int FromHour { get; private set; }

    /// <summary>Hora de cierre (0-24, mayor que FromHour).</summary>
    public int ToHour { get; private set; }
}
