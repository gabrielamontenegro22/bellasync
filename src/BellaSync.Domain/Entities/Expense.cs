using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Egreso (gasto) del día del salón. Plata que SALE — opuesto del
/// Payment. Ejemplos típicos:
///   - Compra de insumos al proveedor en efectivo.
///   - Domicilio del almuerzo del equipo.
///   - Propina pagada de la caja a un estilista al final del día.
///   - Pago de servicios menores (agua de garrafón, mensajería).
///
/// Por qué importa modelarlo:
///   - Sin egresos, el arqueo de efectivo del cierre de caja miente:
///     "esperado = base + ventas efectivo" pero en la realidad ya
///     se sacó plata de la caja durante el día.
///   - Los reportes mensuales de salud financiera del salón dependen
///     de saber cuánto se gastó.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class Expense : BaseEntity, ITenantEntity
{
    private Expense() { }

    /// <summary>
    /// Factory: crea un egreso. Valida invariantes básicas:
    ///   - Concept no vacío.
    ///   - Amount > 0 (un egreso de $0 no tiene sentido).
    ///
    /// Method default = Cash porque ~95% de los egresos del salón son
    /// en efectivo (compra al proveedor, propinas), y son los únicos
    /// que afectan el arqueo. Pero permitimos otros métodos para
    /// trazabilidad completa cuando se paga por transferencia.
    /// </summary>
    public static Expense Create(
        Guid tenantId,
        string concept,
        Money amount,
        PaymentMethod method,
        string? provider,
        Guid? registeredByUserId,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (string.IsNullOrWhiteSpace(concept))
            throw new DomainException("El concepto del egreso es obligatorio.");
        if (amount.Amount <= 0m)
            throw new DomainException("El monto del egreso debe ser mayor a cero.");

        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();

        if (method == PaymentMethod.Transfer && normalizedProvider is null)
            throw new DomainException("Para Transferencia hay que indicar el banco o billetera.");
        if (method == PaymentMethod.Cash && normalizedProvider is not null)
            throw new DomainException("Efectivo no lleva proveedor.");

        return new Expense
        {
            TenantId = tenantId,
            Concept = concept.Trim(),
            Amount = amount,
            Method = method,
            Provider = normalizedProvider,
            RegisteredByUserId = registeredByUserId,
            RegisteredAt = utcNow,
        };
    }

    /// <summary>Plumbing multi-tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Texto libre que explica el egreso. La admin lo lee literal en el
    /// resumen del día y en el historial de cierres. Ej: "Compra
    /// tintes Wella (proveedor)", "Domicilio almuerzo equipo".
    /// </summary>
    public string Concept { get; private set; } = string.Empty;

    /// <summary>Monto del egreso (Money asegura non-negativo).</summary>
    public Money Amount { get; private set; } = Money.Zero;

    /// <summary>
    /// Método con el que se pagó. Por default Cash. Si es Cash, el monto
    /// se descuenta del efectivo esperado en el arqueo; si no, solo
    /// queda registrado para reportes.
    /// </summary>
    public PaymentMethod Method { get; private set; }

    /// <summary>
    /// Banco o billetera (cuando Method=Transfer) o marca de tarjeta
    /// (cuando Method=Card). null si Cash.
    /// </summary>
    public string? Provider { get; private set; }

    /// <summary>
    /// Quién registró el egreso. null si fue un proceso automático.
    /// Hoy siempre es el User loggeado de la recepción.
    /// </summary>
    public Guid? RegisteredByUserId { get; private set; }

    /// <summary>Nav property al user que registró (para mostrar nombre en UI).</summary>
    public User? RegisteredByUser { get; private set; }

    /// <summary>Cuándo se registró (UTC).</summary>
    public DateTime RegisteredAt { get; private set; }

    // ===== MÉTODOS VERBALES =====

    /// <summary>
    /// ¿Salió de la caja en efectivo? Necesario para el arqueo:
    /// solo los egresos cash reducen el esperado.
    /// </summary>
    public bool IsCash() => Method == PaymentMethod.Cash;

    /// <summary>
    /// Corrige el concepto (típico: la admin escribió "domicilio"
    /// y quería "Domicilio almuerzo equipo"). No cambia monto ni
    /// método — para eso, anular y crear de nuevo.
    /// </summary>
    public void UpdateConcept(string concept)
    {
        if (string.IsNullOrWhiteSpace(concept))
            throw new DomainException("El concepto del egreso es obligatorio.");
        Concept = concept.Trim();
    }
}
