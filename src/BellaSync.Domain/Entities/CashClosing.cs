using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Cierre de caja de un día. Se crea cuando la admin termina el día
/// y confirma el arqueo. Hay máximo UNO por (tenant, día).
///
/// Persistir esto es lo que diferencia "marqué cerrado en la pantalla"
/// (que se perdía al refresh) de "este día quedó auditado". El historial
/// permite revisar cierres anteriores y notas explicativas si la
/// diferencia no cuadró.
///
/// Snapshot rico — guardamos los montos del día al momento del cierre,
/// no solo el conteo del efectivo. Si después se agregan/borran payments
/// del día, el historial del cierre sigue mostrando lo que se vio cuando
/// se firmó.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class CashClosing : BaseEntity, ITenantEntity
{
    private CashClosing() { }

    /// <summary>
    /// Factory: registra el cierre del día. Valida invariantes:
    ///   - ClosedDate no en el futuro (no cerrás mañana hoy).
    ///   - Counted &gt;= 0 (no podés haber contado plata negativa).
    ///   - Si Diff != 0, DiffNote obligatorio — la regla del negocio
    ///     que vino del frontend, replicada en dominio.
    /// </summary>
    public static CashClosing Create(
        Guid tenantId,
        DateOnly closedDate,
        DateOnly todayColombia,
        Money baseAmount,
        Money cashSales,
        Money cashExpenses,
        Money totalAmount,
        Money countedCash,
        string? diffNote,
        Guid? closedByUserId,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (closedDate > todayColombia)
            throw new DomainException("No se puede cerrar la caja de un día futuro.");

        // expected = base + cashSales − cashExpenses. Money no acepta negativos,
        // así que si los egresos cash superan a la base + ventas (raro pero posible
        // en un día sin actividad y muchos egresos), clamp a 0 y la admin verá
        // un diff negativo grande que será evidente.
        var expectedRaw = baseAmount.Amount + cashSales.Amount - cashExpenses.Amount;
        var expected = expectedRaw < 0m ? Money.Zero : Money.Create(expectedRaw);

        var diffValue = countedCash.Amount - expected.Amount;

        var normalizedNote = string.IsNullOrWhiteSpace(diffNote) ? null : diffNote.Trim();
        if (diffValue != 0m && normalizedNote is null)
            throw new DomainException("La diferencia requiere una nota explicativa.");

        return new CashClosing
        {
            TenantId = tenantId,
            ClosedDate = closedDate,
            BaseAmount = baseAmount,
            CashSales = cashSales,
            CashExpenses = cashExpenses,
            ExpectedCash = expected,
            CountedCash = countedCash,
            Diff = diffValue,
            DiffNote = normalizedNote,
            TotalAmount = totalAmount,
            ClosedAt = utcNow,
            ClosedByUserId = closedByUserId,
        };
    }

    public Guid TenantId { get; set; }

    /// <summary>El día (zona Colombia) que este cierre representa.</summary>
    public DateOnly ClosedDate { get; private set; }

    // ===== Snapshot del día =====

    /// <summary>Base inicial de la caja para ese día.</summary>
    public Money BaseAmount { get; private set; } = Money.Zero;

    /// <summary>Ventas en efectivo del día (snapshot al cierre).</summary>
    public Money CashSales { get; private set; } = Money.Zero;

    /// <summary>Egresos en efectivo del día (snapshot al cierre).</summary>
    public Money CashExpenses { get; private set; } = Money.Zero;

    /// <summary>Esperado en caja = Base + CashSales − CashExpenses.</summary>
    public Money ExpectedCash { get; private set; } = Money.Zero;

    /// <summary>Lo que la admin contó físicamente.</summary>
    public Money CountedCash { get; private set; } = Money.Zero;

    /// <summary>
    /// CountedCash − ExpectedCash. Puede ser negativo (faltó plata),
    /// positivo (sobró) o cero (cuadró). Por eso es decimal y no Money
    /// (Money no acepta negativos).
    /// </summary>
    public decimal Diff { get; private set; }

    /// <summary>
    /// Nota explicativa de la diferencia. Obligatoria si Diff != 0,
    /// null si cuadró perfecto.
    /// </summary>
    public string? DiffNote { get; private set; }

    /// <summary>
    /// Total recaudado en TODOS los métodos (efectivo + transferencias
    /// + tarjeta). Snapshot informativo — útil para el historial donde
    /// la admin quiere ver "el día X total fue $Y".
    /// </summary>
    public Money TotalAmount { get; private set; } = Money.Zero;

    /// <summary>UTC del momento exacto en que se firmó el cierre.</summary>
    public DateTime ClosedAt { get; private set; }

    /// <summary>Quién firmó el cierre (admin/recepcionista).</summary>
    public Guid? ClosedByUserId { get; private set; }

    /// <summary>Nav property al user que cerró (para mostrar nombre en historial).</summary>
    public User? ClosedByUser { get; private set; }
}
