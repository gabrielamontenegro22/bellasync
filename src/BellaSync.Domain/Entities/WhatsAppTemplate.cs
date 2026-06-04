using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Plantilla de mensaje WhatsApp que el salón puede activar y editar.
///
/// Modelo simplificado: UNA plantilla por (TenantId, Kind). No queremos
/// que la admin tenga que decidir entre "Recordatorio v2 (con emoji)"
/// y "Recordatorio v1" — confunde. Una sola, la editan.
///
/// Variables soportadas en el Body (renderer las reemplaza al despachar):
///   {nombre}     → Customer.FullName
///   {fecha}      → "sáb 7 jun"        (formato corto local)
///   {hora}       → "3:00 pm"
///   {servicio}   → Service.Name
///   {anticipo}   → "$80.000"          (Money.Format)
///   {direccion}  → Tenant.Address     (puede ser null → se reemplaza vacío)
///   {salon}      → Tenant.Name
///   {limite}     → "las 12:00 pm"     (hold expiration)
///
/// Body se guarda con los placeholders crudos. El renderizado se hace
/// en el dispatcher justo antes de mandar — así, si la admin cambia el
/// nombre del salón, los próximos mensajes ya salen con el nombre nuevo
/// sin tener que regenerar los pendientes.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class WhatsAppTemplate : BaseEntity, ITenantEntity
{
    private WhatsAppTemplate() { }

    /// <summary>
    /// Factory para crear una plantilla nueva (típicamente seed al onboarding).
    /// La admin después solo hace UpdateBody / Toggle, no crea nuevas.
    /// </summary>
    public static WhatsAppTemplate Create(
        Guid tenantId,
        WhatsAppTemplateKind kind,
        string body,
        bool isEnabled,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("El cuerpo de la plantilla no puede estar vacío.");
        if (body.Length > 1000)
            throw new DomainException("El cuerpo de la plantilla excede el máximo (1000 caracteres).");

        return new WhatsAppTemplate
        {
            TenantId = tenantId,
            Kind = kind,
            Body = body.Trim(),
            IsEnabled = isEnabled,
            UpdatedAt = utcNow,  // hereda de BaseEntity (DateTime?)
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>Tipo único dentro del tenant — define cuándo se dispara.</summary>
    public WhatsAppTemplateKind Kind { get; private set; }

    /// <summary>
    /// Texto con placeholders. Se valida solo longitud — la sintaxis de
    /// placeholders es liberal (el renderer ignora desconocidos).
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Si false, el dispatcher salta este Kind. La admin puede prender/
    /// apagar tipos individuales (ej: desactivar Cumpleaños pero dejar
    /// Recordatorio 24h).
    /// </summary>
    public bool IsEnabled { get; private set; }

    // UpdatedAt heredado de BaseEntity (DateTime?).

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// Cambia el texto. Se valida igual que en Create.
    /// </summary>
    public void UpdateBody(string body, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("El cuerpo de la plantilla no puede estar vacío.");
        if (body.Length > 1000)
            throw new DomainException("El cuerpo de la plantilla excede el máximo (1000 caracteres).");

        Body = body.Trim();
        UpdatedAt = utcNow;
    }

    /// <summary>Prende/apaga el envío automático de este tipo.</summary>
    public void SetEnabled(bool enabled, DateTime utcNow)
    {
        if (IsEnabled == enabled) return;
        IsEnabled = enabled;
        UpdatedAt = utcNow;
    }
}
