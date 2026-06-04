namespace BellaSync.Application.Features.Commissions.Dtos;

/// <summary>
/// Resumen de comisiones para una fecha. Una fila por estilista con
/// todo lo necesario para que la admin decida si pagar o no.
/// </summary>
public class CommissionsSummaryResponse
{
    /// <summary>Rango consultado (YYYY-MM-DD).</summary>
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;

    public List<StylistCommissionRow> Stylists { get; set; } = new();

    /// <summary>Totales agregados de todos los estilistas — útil para el header.</summary>
    public decimal TotalEarned { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
}

public class StylistCommissionRow
{
    public Guid StylistId { get; set; }
    public string StylistName { get; set; } = string.Empty;
    public string? StylistColor { get; set; }

    /// <summary>Cantidad de pagos del período asociados al estilista.</summary>
    public int PaymentsCount { get; set; }

    /// <summary>Suma de Payment.Amount (sin propinas) de esos pagos.</summary>
    public decimal CobradoTotal { get; set; }

    /// <summary>
    /// Comisión derivada en el período: Σ(payment.amount * service.commissionPct).
    /// Se calcula en memoria con el snapshot del % del servicio actual
    /// (la próxima versión podrá snapshotear el % en cada Payment para
    /// no tocar comisiones viejas si cambia el %).
    /// </summary>
    public decimal CommissionEarned { get; set; }

    /// <summary>
    /// Total que YA se le pagó en CommissionPayouts cuyo período
    /// está total o parcialmente dentro del rango consultado. Incluye
    /// payouts con period_to anterior a from para cubrir comisiones
    /// que se quedaron atrás (se restarán del Earned solo en la
    /// proporción correspondiente — para v1 simple, se restan completos).
    /// </summary>
    public decimal AlreadyPaidInRange { get; set; }

    /// <summary>Earned − AlreadyPaidInRange. Lo que falta liquidar.</summary>
    public decimal Pending { get; set; }
}
