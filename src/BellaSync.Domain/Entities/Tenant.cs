using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Representa un salón de belleza dentro del SaaS.
/// El Tenant es el "dueño" lógico de los datos: usuarios, citas,
/// inventario, clientes, pagos, etc. siempre pertenecen a un Tenant.
///
/// Setters privados: la entidad solo se muta vía métodos verbales
/// (`Rename`, `Deactivate`, `Reactivate`).
/// </summary>
public class Tenant : BaseEntity
{
    private Tenant() { }

    /// <summary>
    /// Factory: crea un tenant nuevo con invariantes validadas.
    /// El slug se valida pero NO se genera acá — la responsabilidad de
    /// generar slugs únicos pertenece al caller (que necesita acceso a
    /// la BD para verificar colisiones).
    /// </summary>
    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del salón es obligatorio.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("El slug del salón es obligatorio.");

        var tenant = new Tenant();
        tenant.Name = name.Trim();
        tenant.Slug = slug;
        tenant.IsActive = true;
        // Defaults conservadores. Los heredamos de los valores históricos
        // de appsettings.json para no romper salones existentes.
        tenant.HoldDurationHours = 3;
        tenant.HoldMinBeforeAppointmentMinutes = 30;
        tenant.MinAdvanceMinutes = 30;
        // Comisiones OFF por default — no asumimos que el salón paga
        // por comisión. La admin decide activarlo desde Configuración
        // cuando le sirve (sueldos fijos, alquiler de silla, etc.).
        tenant.CommissionsEnabled = false;
        return tenant;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador único legible para URLs (ej. "bella-spa-neiva").
    /// Se genera fuera de la entidad (típicamente con SlugGenerator) porque
    /// requiere chequeo de unicidad contra la BD.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    // ===== POLÍTICA DE PAGOS DEL SALÓN =====
    // Estos valores eran globales (IOptions<AppointmentSettings>) pero
    // cada salón los puede ajustar — un spa de lujo quizá da 24h para
    // pagar, una peluquería express solo 1h.

    /// <summary>
    /// Horas máximas que un cupo queda reservado tras agendar antes
    /// de cancelar la cita por falta de pago. Default: 3h.
    /// </summary>
    public int HoldDurationHours { get; private set; } = 3;

    /// <summary>
    /// Minutos antes de la cita en que el hold deja de aplicar (si
    /// el cliente no pagó hasta entonces, se cancela). Default: 30.
    /// Sirve para no liberar cupos a último minuto: si la cita es a
    /// las 10:00 y este valor es 30, a las 9:30 la cita queda firme
    /// o se cancela.
    /// </summary>
    public int HoldMinBeforeAppointmentMinutes { get; private set; } = 30;

    /// <summary>
    /// Minutos mínimos de anticipación con que se puede agendar una
    /// cita. Default: 30. SalonAdmin puede saltar esta regla para
    /// walk-ins imprevistos.
    /// </summary>
    public int MinAdvanceMinutes { get; private set; } = 30;

    // ===== COMISIONES (opt-in) =====

    /// <summary>
    /// Si el salón quiere llevar registro de comisiones por estilista.
    /// OFF por default — muchos salones pagan sueldo fijo o trabajan
    /// con alquiler de silla y no necesitan esto. Cuando está OFF, el
    /// módulo de Comisiones (pantalla, sidebar item, campo % en
    /// servicios) queda invisible. La admin lo activa desde
    /// Configuración → Comisiones cuando lo necesita.
    /// </summary>
    public bool CommissionsEnabled { get; private set; }

    // Relación inversa: usuarios que pertenecen a este salón
    public ICollection<User> Users { get; private set; } = new List<User>();

    // ===== MÉTODOS VERBALES =====

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre del salón es obligatorio.");
        Name = newName.Trim();
    }

    /// <summary>
    /// Desactivar el salón. Los users del salón no podrán iniciar sesión
    /// (Login chequea Tenant.IsActive). Idempotente.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivar el salón. Idempotente.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Actualiza la política de pagos del salón. Valida que todos los
    /// valores sean positivos y razonables (no permitimos hold de
    /// 0 horas, ni anticipación de 1 año).
    /// </summary>
    public void UpdatePaymentPolicy(
        int holdDurationHours,
        int holdMinBeforeAppointmentMinutes,
        int minAdvanceMinutes)
    {
        if (holdDurationHours < 1 || holdDurationHours > 48)
            throw new DomainException("La duración del hold debe estar entre 1 y 48 horas.");
        if (holdMinBeforeAppointmentMinutes < 0 || holdMinBeforeAppointmentMinutes > 240)
            throw new DomainException("Los minutos antes de la cita deben estar entre 0 y 240.");
        if (minAdvanceMinutes < 0 || minAdvanceMinutes > 1440)
            throw new DomainException("La anticipación mínima debe estar entre 0 y 1440 minutos (24h).");

        HoldDurationHours = holdDurationHours;
        HoldMinBeforeAppointmentMinutes = holdMinBeforeAppointmentMinutes;
        MinAdvanceMinutes = minAdvanceMinutes;
    }

    /// <summary>
    /// Activa o desactiva el módulo de comisiones. Cambiarlo es
    /// idempotente y no afecta datos históricos: si se apaga después
    /// de tener payouts hechos, los payouts viejos siguen existiendo
    /// pero la pantalla deja de ser visible. Si se reactiva, el
    /// historial vuelve a verse intacto.
    /// </summary>
    public void SetCommissionsEnabled(bool enabled)
    {
        CommissionsEnabled = enabled;
    }
}
