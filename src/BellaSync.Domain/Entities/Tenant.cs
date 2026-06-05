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
        // Defaults conservadores de permisos para recepción. Asumen que
        // la admin todavía no decidió, así que arrancamos restrictivo:
        // tope chico de egresos, recepción puede cancelar con plata pero
        // siempre con nota, y NO puede cerrar caja (la admin firma).
        tenant.ReceptionExpenseCapCop = 100_000m;
        tenant.ReceptionCanCancelWithMoney = true;
        tenant.ReceptionCanCloseCash = false;
        // Permisos granulares — todos OFF por default. La admin los
        // activa selectivamente según confíe en cada miembro del equipo.
        tenant.ReceptionCanEditStylists = false;
        tenant.ReceptionCanEditServices = false;
        tenant.ReceptionCanViewReports = false;
        tenant.ReceptionCanViewCommissions = false;
        tenant.ReceptionCanEditSchedule = false;
        tenant.ReceptionCanEditPaymentPolicy = false;
        tenant.ReceptionCanEditSalonInfo = false;
        tenant.ReceptionCanEditInventory = false;
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

    // ===== HORARIO DEL SALÓN (flags) =====
    // Los horarios de cada día y los cierres puntuales viven en tablas
    // aparte (SalonWeeklyHours, SalonClosedDate). Acá quedan los flags
    // simples que pertenecen al tenant.

    /// <summary>
    /// Si está activo, la agenda bloquea reservas en la franja
    /// LunchBreakFromHour–LunchBreakToHour todos los días.
    /// </summary>
    public bool LunchBreakEnabled { get; private set; }

    /// <summary>Hora de inicio del bloqueo de almuerzo (0-24).</summary>
    public int LunchBreakFromHour { get; private set; } = 13;

    /// <summary>Hora de fin del bloqueo de almuerzo (0-24, &gt; from).</summary>
    public int LunchBreakToHour { get; private set; } = 14;

    /// <summary>
    /// Si está activo, los festivos nacionales de Colombia se tratan
    /// como días cerrados automáticamente (sin necesidad de agregarlos
    /// uno por uno en SalonClosedDate).
    /// </summary>
    public bool IsHolidaysClosed { get; private set; }

    // ===== PERMISOS DE RECEPCIÓN (configurables por la admin) =====
    // Cada salón es distinto: en uno la recepcionista compra los tintes
    // y maneja plata; en otro solo agenda y cobra. Estos toggles
    // permiten que la admin ajuste qué puede hacer recepción sin
    // tener que pedirle ayuda a soporte.

    /// <summary>
    /// Tope (en COP) de egresos que recepción puede registrar sin admin.
    /// - null  → sin límite (recepción confiable, puede registrar lo que sea).
    /// - 0     → recepción NO puede registrar egresos.
    /// - X &gt; 0 → cap; sobre este monto requiere admin.
    /// La admin no tiene cap NUNCA, independiente del valor.
    /// </summary>
    public decimal? ReceptionExpenseCapCop { get; private set; }

    /// <summary>
    /// Si recepción puede cancelar citas que tengan plata asociada
    /// (Payments o vouchers Validados). True por default — bloquear
    /// totalmente es excesivo, hay casos legítimos (cliente avisa
    /// que no puede venir y pide cancelar). Cuando es true, el
    /// frontend exige una nota explicativa obligatoria para que
    /// admin sepa qué hacer con el dinero (devolver/crédito/perder).
    /// Si es false, recepción ve un alert y debe pedirle a admin.
    /// </summary>
    public bool ReceptionCanCancelWithMoney { get; private set; } = true;

    /// <summary>
    /// Si recepción puede firmar el cierre de caja del día. False por
    /// default — el cierre es decisión financiera de fin de día y
    /// suele hacerlo la admin. Pero hay salones donde la admin no
    /// pasa por el local y delega esto a una recepción de confianza.
    /// </summary>
    public bool ReceptionCanCloseCash { get; private set; }

    // ---- Catálogo del salón ----

    /// <summary>
    /// Si recepción puede crear/editar/borrar estilistas + marcar sus
    /// días libres. False por default — el equipo es decisión de admin.
    /// </summary>
    public bool ReceptionCanEditStylists { get; private set; }

    /// <summary>
    /// Si recepción puede crear/editar/borrar servicios y cambiar
    /// precios. False por default — oferta y precios son decisión admin.
    /// </summary>
    public bool ReceptionCanEditServices { get; private set; }

    // ---- Información sensible (KPIs financieros) ----

    /// <summary>
    /// Si recepción puede ver /reportes (facturación, KPIs, tendencias).
    /// False por default — son datos financieros sensibles.
    /// </summary>
    public bool ReceptionCanViewReports { get; private set; }

    /// <summary>
    /// Si recepción puede ver /comisiones (cuánto le toca a cada
    /// estilista, liquidaciones). False por default — info sensible.
    /// </summary>
    public bool ReceptionCanViewCommissions { get; private set; }

    // ---- Configuración del salón ----

    /// <summary>
    /// Si recepción puede editar /configuracion/horario.
    /// False por default — el horario lo decide admin.
    /// </summary>
    public bool ReceptionCanEditSchedule { get; private set; }

    /// <summary>
    /// Si recepción puede editar /configuracion/pagos (tiempos de hold,
    /// anticipación mínima). False por default.
    /// </summary>
    public bool ReceptionCanEditPaymentPolicy { get; private set; }

    /// <summary>
    /// Si recepción puede editar /configuracion/general (nombre,
    /// dirección, logo, info pública). False por default.
    /// </summary>
    public bool ReceptionCanEditSalonInfo { get; private set; }

    /// <summary>
    /// Si recepción puede tocar el módulo de Inventario: crear productos,
    /// editarlos, archivarlos, y registrar movimientos (entradas, salidas,
    /// ajustes). False por default — el inventario es trazabilidad de plata
    /// (cuánto vale el stock, cuándo se agota algo caro), típicamente
    /// admin lo lleva. Pero hay salones donde la recepcionista recibe
    /// proveedores y registra entradas — ahí la admin activa este toggle.
    /// </summary>
    public bool ReceptionCanEditInventory { get; private set; }

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

    /// <summary>
    /// Actualiza los flags del horario (la franja de almuerzo y si los
    /// festivos cuentan como cerrados). Las horas semanales y los
    /// cierres puntuales se manejan vía SalonWeeklyHours/SalonClosedDate
    /// porque son colecciones, no propiedades simples.
    /// </summary>
    public void UpdateScheduleFlags(
        bool lunchEnabled,
        int lunchFromHour,
        int lunchToHour,
        bool holidaysClosed)
    {
        if (lunchEnabled)
        {
            if (lunchFromHour < 0 || lunchFromHour > 24)
                throw new DomainException("Hora de almuerzo: from debe estar entre 0 y 24.");
            if (lunchToHour < 0 || lunchToHour > 24)
                throw new DomainException("Hora de almuerzo: to debe estar entre 0 y 24.");
            if (lunchFromHour >= lunchToHour)
                throw new DomainException("Hora de almuerzo: from debe ser anterior a to.");
        }

        LunchBreakEnabled = lunchEnabled;
        LunchBreakFromHour = lunchFromHour;
        LunchBreakToHour = lunchToHour;
        IsHolidaysClosed = holidaysClosed;
    }

    /// <summary>
    /// Actualiza TODOS los permisos de recepción del salón en una pasada.
    /// Pasamos el set completo (no parcial) para que el dominio refleje
    /// el estado del form de la admin tal como lo ve. Validación mínima
    /// porque son booleans + un decimal opcional.
    /// </summary>
    public void UpdateReceptionPermissions(
        // Operación diaria
        decimal? expenseCapCop,
        bool canCancelWithMoney,
        bool canCloseCash,
        // Catálogo
        bool canEditStylists,
        bool canEditServices,
        bool canEditInventory,
        // Info sensible
        bool canViewReports,
        bool canViewCommissions,
        // Configuración
        bool canEditSchedule,
        bool canEditPaymentPolicy,
        bool canEditSalonInfo)
    {
        if (expenseCapCop is not null && expenseCapCop < 0m)
            throw new DomainException(
                "El tope de egresos no puede ser negativo. Dejá 0 si querés bloquear o vacío si querés sin límite.");

        ReceptionExpenseCapCop = expenseCapCop;
        ReceptionCanCancelWithMoney = canCancelWithMoney;
        ReceptionCanCloseCash = canCloseCash;
        ReceptionCanEditStylists = canEditStylists;
        ReceptionCanEditServices = canEditServices;
        ReceptionCanEditInventory = canEditInventory;
        ReceptionCanViewReports = canViewReports;
        ReceptionCanViewCommissions = canViewCommissions;
        ReceptionCanEditSchedule = canEditSchedule;
        ReceptionCanEditPaymentPolicy = canEditPaymentPolicy;
        ReceptionCanEditSalonInfo = canEditSalonInfo;
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
