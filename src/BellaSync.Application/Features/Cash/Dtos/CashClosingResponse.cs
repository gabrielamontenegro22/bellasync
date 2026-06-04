namespace BellaSync.Application.Features.Cash.Dtos;

/// <summary>
/// Vista del historial de cierres + estado actual ("¿hay cierre para hoy?").
/// Refleja exactamente el snapshot que se firmó.
/// </summary>
public class CashClosingResponse
{
    public Guid Id { get; set; }

    /// <summary>YYYY-MM-DD (zona Colombia) — el día cerrado.</summary>
    public string ClosedDate { get; set; } = string.Empty;

    public decimal BaseAmount { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashExpenses { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    /// <summary>Counted − Expected. Negativo = faltó, positivo = sobró, 0 = cuadró.</summary>
    public decimal Diff { get; set; }
    public string? DiffNote { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>UTC del momento exacto del cierre.</summary>
    public DateTime ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    /// <summary>Nombre del user que firmó el cierre (para historial).</summary>
    public string? ClosedByUserName { get; set; }
}
