using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Liquidación de comisiones a un estilista. Representa **dinero que
/// salió del salón hacia el estilista** para cubrir las comisiones
/// acumuladas hasta una fecha.
///
/// Cómo se usa:
///   1. La admin abre /comisiones y ve "Andrea acumuló $450k al 15-jun".
///   2. Le paga por fuera (efectivo, transferencia) y marca pagado
///      en BellaSync → se crea un CommissionPayout con StylistId=Andrea,
///      Amount=$450k, PeriodTo=15-jun.
///   3. Próxima vez que abra /comisiones, las comisiones derivadas de
///      pagos anteriores al 15-jun ya están "cubiertas" por este payout
///      y solo cuenta las nuevas (16-jun en adelante).
///
/// El monto se snapshea al momento de pagar — si después se cambia el %
/// de comisión del servicio, el payout viejo no se recalcula.
///
/// Setters privados — toda mutación pasa por métodos verbales.
/// </summary>
public class CommissionPayout : BaseEntity, ITenantEntity
{
    private CommissionPayout() { }

    /// <summary>
    /// Factory: crea una liquidación. Valida invariantes:
    ///   - StylistId obligatorio.
    ///   - Amount > 0 (un payout de $0 no tiene sentido).
    ///   - PeriodFrom &lt;= PeriodTo.
    /// </summary>
    public static CommissionPayout Create(
        Guid tenantId,
        Guid stylistId,
        Money amount,
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? paidByUserId,
        string? notes,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("TenantId es obligatorio.");
        if (stylistId == Guid.Empty)
            throw new DomainException("StylistId es obligatorio.");
        if (amount.Amount <= 0m)
            throw new DomainException("El monto del payout debe ser mayor a cero.");
        if (periodFrom > periodTo)
            throw new DomainException("PeriodFrom no puede ser posterior a PeriodTo.");

        return new CommissionPayout
        {
            TenantId = tenantId,
            StylistId = stylistId,
            Amount = amount,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            PaidAt = utcNow,
            PaidByUserId = paidByUserId,
            Notes = NormalizeOptional(notes),
        };
    }

    /// <summary>Plumbing multi-tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>A quién se le liquidó.</summary>
    public Guid StylistId { get; private set; }
    public Stylist? Stylist { get; private set; }

    /// <summary>Cuánto se pagó. Snapshot al momento de liquidar.</summary>
    public Money Amount { get; private set; } = Money.Zero;

    /// <summary>
    /// Rango de fechas cubierto por la liquidación. Los pagos con
    /// RegisteredAt entre PeriodFrom 00:00 y PeriodTo 23:59 (zona
    /// Colombia) están cubiertos.
    /// </summary>
    public DateOnly PeriodFrom { get; private set; }
    public DateOnly PeriodTo { get; private set; }

    /// <summary>Cuándo se hizo el payout (UTC).</summary>
    public DateTime PaidAt { get; private set; }

    /// <summary>Quién registró el payout (admin del salón típicamente).</summary>
    public Guid? PaidByUserId { get; private set; }

    /// <summary>
    /// Nota libre — ej. "Pagado en efectivo a Andrea el 16-jun" o
    /// "Transferencia Nequi ref TRF-238". Ayuda al cierre de auditoría.
    /// </summary>
    public string? Notes { get; private set; }

    public void UpdateNotes(string? notes) => Notes = NormalizeOptional(notes);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
