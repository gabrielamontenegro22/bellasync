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
///
/// Setters privados: el cliente solo se muta vía métodos verbales
/// (`UpdateContact`, `Archive`, `Reactivate`, etc.).
/// </summary>
public class Customer : BaseEntity, ITenantEntity
{
    private Customer() { }

    /// <summary>Factory: crea un cliente nuevo con invariantes validadas.</summary>
    public static Customer Create(
        Guid tenantId,
        string fullName,
        string phone,
        string? email = null,
        DateOnly? birthday = null,
        string? documentNumber = null,
        string? address = null,
        string? notes = null,
        bool acceptsMarketing = false)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre del cliente es obligatorio.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("El teléfono del cliente es obligatorio.");

        var customer = new Customer { TenantId = tenantId };
        customer.FullName = fullName.Trim();
        customer.Phone = NormalizePhone(phone);
        customer.Email = NormalizeOptional(email)?.ToLowerInvariant();
        customer.Birthday = birthday;
        customer.DocumentNumber = NormalizeOptional(documentNumber);
        customer.Address = NormalizeOptional(address);
        customer.Notes = NormalizeOptional(notes);
        customer.AcceptsMarketing = acceptsMarketing;
        customer.IsActive = true;
        return customer;
    }

    // Plumbing multi-tenant (ver Service.cs para la justificación).
    public Guid TenantId { get; set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto. Único entre clientes ACTIVOS del salón.
    /// Es el identificador de hecho que usa la recepción para buscarlos.
    /// </summary>
    public string Phone { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    /// <summary>
    /// Fecha de nacimiento. Útil para promociones de cumpleaños.
    /// Solo guardamos día/mes/año (sin hora).
    /// </summary>
    public DateOnly? Birthday { get; private set; }

    /// <summary>Documento de identidad (cédula). Opcional.</summary>
    public string? DocumentNumber { get; private set; }

    public string? Address { get; private set; }

    /// <summary>
    /// Notas internas del salón sobre el cliente.
    /// Texto libre — preferencias, observaciones, comentarios del estilista.
    /// "No le gusta el agua muy caliente", "prefiere atención con Andrea", etc.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// El cliente aceptó recibir comunicaciones de marketing
    /// (recordatorios, promos, cumpleaños) por WhatsApp/email.
    /// Por defecto false — opt-in explícito.
    /// </summary>
    public bool AcceptsMarketing { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ===== MÉTODOS VERBALES =====

    /// <summary>Cambia el nombre. Valida que no sea vacío.</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre del cliente es obligatorio.");
        FullName = newName.Trim();
    }

    /// <summary>
    /// Actualiza toda la información de contacto en una operación cohesiva.
    /// Phone es obligatorio; el resto puede ser null para borrarlo.
    /// </summary>
    public void UpdateContact(string phone, string? email, string? address)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("El teléfono del cliente es obligatorio.");
        Phone = NormalizePhone(phone);
        Email = NormalizeOptional(email)?.ToLowerInvariant();
        Address = NormalizeOptional(address);
    }

    /// <summary>Cambia los datos administrativos (cédula, cumpleaños, notas).</summary>
    public void UpdateProfile(string? documentNumber, DateOnly? birthday, string? notes)
    {
        DocumentNumber = NormalizeOptional(documentNumber);
        Birthday = birthday;
        Notes = NormalizeOptional(notes);
    }

    /// <summary>Acepta recibir comunicaciones de marketing.</summary>
    public void OptInMarketing() => AcceptsMarketing = true;

    /// <summary>Retira el consentimiento de marketing.</summary>
    public void OptOutMarketing() => AcceptsMarketing = false;

    /// <summary>Soft delete. Idempotente.</summary>
    public void Archive() => IsActive = false;

    /// <summary>Reactivar un cliente archivado. Idempotente.</summary>
    public void Reactivate() => IsActive = true;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Normaliza un teléfono colombiano para usar como identidad canónica
    /// (deduplicación). Quita espacios, guiones, paréntesis, puntos y el
    /// prefijo +57 (que está implícito si el número tiene 10 dígitos
    /// arrancando en 3, formato celular colombiano).
    ///
    /// Ejemplos:
    ///   "+57 318 555 1234" → "3185551234"
    ///   "318-555-1234"     → "3185551234"
    ///   "(318) 555 1234"   → "3185551234"
    ///   "3185551234"       → "3185551234"  (ya canónico)
    ///
    /// Caso B2 del audit: antes "318 555 1234" y "+57 318 555 1234" se
    /// veían como dos clientes distintos. Esta normalización debe usarse
    /// en CreateCustomer / UpdateCustomer / búsquedas para evitar duplicados.
    /// </summary>
    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        // Solo dígitos (descarta espacios, +, -, (, ), ., etc).
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // Prefijo país Colombia opcional.
        if (digits.StartsWith("57") && digits.Length == 12)
            digits = digits[2..];

        return digits;
    }
}
