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

    // ===== INFORMACIÓN GENERAL DEL SALÓN =====
    // Todos opcionales. Se usan en: portal público de booking (logo,
    // dirección), confirmaciones por WhatsApp (nombre, teléfono),
    // factura (todos), etc.

    /// <summary>Dirección física del salón. Ej: "Cra 25 #18-32, Neiva".</summary>
    public string? Address { get; private set; }

    /// <summary>Teléfono del salón (no del WhatsApp, el del local).</summary>
    public string? Phone { get; private set; }

    /// <summary>Email de contacto público del salón.</summary>
    public string? ContactEmail { get; private set; }

    /// <summary>URL pública del logo del salón (CDN, Imgur, lo que sea).</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Handle de Instagram sin @ (ej: "bella.spa.neiva").</summary>
    public string? InstagramHandle { get; private set; }

    /// <summary>Descripción corta del salón. Aparece en el portal público de booking.</summary>
    public string? Description { get; private set; }

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

    /// <summary>
    /// Actualiza la info pública/contacto del salón. Todos los campos
    /// son opcionales — pasar null en cualquiera lo borra. Validaciones:
    ///   - Address max 200, Phone max 30, ContactEmail max 150 y debe
    ///     parecer un email, LogoUrl max 500 y debe empezar con http,
    ///     InstagramHandle max 50 sin @, Description max 500.
    /// El Name se cambia por separado vía Rename() — está en otro
    /// "concepto" (identidad legal vs info pública).
    /// </summary>
    public void UpdateInfo(
        string? address,
        string? phone,
        string? contactEmail,
        string? logoUrl,
        string? instagramHandle,
        string? description)
    {
        var addr = Normalize(address, 200, nameof(address));
        var ph   = Normalize(phone, 30, nameof(phone));
        var em   = Normalize(contactEmail, 150, nameof(contactEmail));
        if (em is not null && !em.Contains('@'))
            throw new DomainException("El email de contacto no parece válido.");
        var logo = Normalize(logoUrl, 500, nameof(logoUrl));
        if (logo is not null && !logo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                              && !logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("El logo debe ser una URL http(s).");
        var ig = Normalize(instagramHandle, 50, nameof(instagramHandle));
        if (ig is not null && ig.StartsWith('@')) ig = ig[1..];  // tolerante: limpia @
        var desc = Normalize(description, 500, nameof(description));

        Address = addr;
        Phone = ph;
        ContactEmail = em;
        LogoUrl = logo;
        InstagramHandle = ig;
        Description = desc;
    }

    private static string? Normalize(string? value, int maxLen, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length > maxLen)
            throw new DomainException($"{fieldName} no puede pasar de {maxLen} caracteres.");
        return v;
    }
}
