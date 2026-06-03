using BellaSync.Application.Features.Payments.Dtos;

namespace BellaSync.Application.Features.Cash.Dtos;

/// <summary>
/// Resumen de caja para un día. Lo que la admin abre cada noche para
/// cerrar la jornada: cuánto entró total, breakdown por método, lista
/// completa de pagos para reconciliar contra el extracto del banco.
/// </summary>
public class DailyCashSummaryResponse
{
    /// <summary>Fecha YYYY-MM-DD (zona Colombia) que se consultó.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Total recibido (Amount + Tip de todos los payments del día).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Solo propinas — info para auditoría.</summary>
    public decimal TotalTips { get; set; }

    /// <summary>Cantidad de pagos registrados ese día.</summary>
    public int PaymentCount { get; set; }

    /// <summary>
    /// Breakdown por método de pago. Sirve para cruzar contra los
    /// movimientos del banco — el total de Bancolombia debería coincidir
    /// con lo que entró a la cuenta Bancolombia ese día.
    /// </summary>
    public List<MethodBreakdownItem> ByMethod { get; set; } = new();

    /// <summary>Lista completa de pagos para drill-down.</summary>
    public List<PaymentResponse> Payments { get; set; } = new();
}

public class MethodBreakdownItem
{
    /// <summary>"Cash" / "Bancolombia" / "Nequi" / etc.</summary>
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Total { get; set; }
}
