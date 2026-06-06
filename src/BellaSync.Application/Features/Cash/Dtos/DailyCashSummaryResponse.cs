using BellaSync.Application.Features.Expenses.Dtos;
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

    /// <summary>
    /// Total de PLATA REAL que entró al banco/caja en el día:
    ///   = sum(Payments.Amount + Tip) + sum(Vouchers Validated externos)
    /// EXCLUYE explícitamente los "Vouchers de Crédito interno", que
    /// representan aplicación de crédito existente (saldo viejo
    /// consumido), no plata nueva entrando.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Solo propinas — info para auditoría.</summary>
    public decimal TotalTips { get; set; }

    /// <summary>
    /// Total de movimientos del día (pagos + vouchers validados externos).
    /// Coincide con la cantidad de filas que se ven en "Transacciones".
    /// </summary>
    public int PaymentCount { get; set; }

    /// <summary>
    /// Total de anticipos validados ese día (sub-total dentro de
    /// TotalAmount). EXCLUYE los créditos internos. Útil para mostrarlo
    /// como métrica separada en la UI: "Cobrado: $X · Anticipos: $Y".
    /// </summary>
    public decimal ValidatedDepositsTotal { get; set; }

    /// <summary>Cantidad de vouchers validados ese día (sin créditos internos).</summary>
    public int ValidatedDepositsCount { get; set; }

    /// <summary>
    /// Total de "crédito interno" aplicado hoy: anticipos viejos que se
    /// usaron como pago de citas nuevas. NO es plata real entrante (la
    /// plata ya entró antes, cuando se pagó el voucher original).
    /// Se muestra aparte para que la admin entienda qué porción del
    /// "ingreso aparente" del día es saldo viejo aplicado.
    /// </summary>
    public decimal InternalCreditTotal { get; set; }

    /// <summary>Cantidad de aplicaciones de crédito interno hoy.</summary>
    public int InternalCreditCount { get; set; }

    /// <summary>
    /// Anticipos retenidos hoy por cancelación tardía (decisión Forfeited).
    /// El salón se quedó con esta plata por política de cancelación —
    /// es ingreso "ganado" pero invisible si no se reporta.
    /// </summary>
    public decimal ForfeitedTodayTotal { get; set; }

    /// <summary>Cantidad de Forfeited hoy.</summary>
    public int ForfeitedTodayCount { get; set; }

    /// <summary>
    /// Items individuales de Forfeited del día para drill-down.
    /// Cada uno = un voucher cuya cita se canceló con decisión "No devolver".
    /// </summary>
    public List<ForfeitedItem> ForfeitedToday { get; set; } = new();

    /// <summary>
    /// Breakdown por método de pago. Sirve para cruzar contra los
    /// movimientos del banco — el total de Bancolombia debería coincidir
    /// con lo que entró a la cuenta Bancolombia ese día.
    /// </summary>
    public List<MethodBreakdownItem> ByMethod { get; set; } = new();

    /// <summary>Lista completa de pagos para drill-down.</summary>
    public List<PaymentResponse> Payments { get; set; } = new();

    /// <summary>
    /// Vouchers validados HOY (anticipos online). El frontend los muestra
    /// en la misma lista de "Transacciones" intercalados con los Payments
    /// por hora, para que la admin vea TODO lo que entró hoy en un mismo
    /// lugar (no solo cobros en sitio).
    ///
    /// Incluye TAMBIÉN los vouchers internos (Bank = "Crédito interno")
    /// para que el frontend los pueda mostrar con etiqueta distinta
    /// ("Aplicación de crédito" vs "Anticipo recibido").
    /// </summary>
    public List<ValidatedVoucherItem> ValidatedVouchersToday { get; set; } = new();

    /// <summary>
    /// Total de egresos del día (todos los métodos). Útil para el KPI
    /// "Egresos del día".
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Egresos pagados específicamente en efectivo. Estos son los que
    /// el arqueo de la caja debe restar del esperado:
    ///   expected_cash = base + cash_sales - cash_expenses
    /// </summary>
    public decimal CashExpenses { get; set; }

    /// <summary>Lista completa de egresos del día.</summary>
    public List<ExpenseResponse> Expenses { get; set; } = new();
}

public class MethodBreakdownItem
{
    /// <summary>"Cash" / "Transfer" / "Card" / "Other"</summary>
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Total { get; set; }

    /// <summary>
    /// Sub-desglose por proveedor dentro de este método. Lista vacía
    /// para Cash (no aplica). Para Transfer/Card permite ver
    /// "$120k Transferencia = $80k Bancolombia + $40k Nequi" y cruzar
    /// contra cada extracto bancario por separado.
    /// </summary>
    public List<ProviderBreakdownItem> ByProvider { get; set; } = new();
}

public class ProviderBreakdownItem
{
    /// <summary>"Bancolombia" / "Nequi" / "Visa" / etc. Puede ser null si el cobro no llevó proveedor.</summary>
    public string? Provider { get; set; }
    public int Count { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// Voucher validado del día, formateado para mostrarse junto a los
/// Payments en la lista de "Transacciones". Permite ver de un vistazo
/// quién pagó qué (cliente + servicio + banco) sin tener que cruzar
/// con la cola de validación.
/// </summary>
public class ValidatedVoucherItem
{
    public Guid VoucherId { get; set; }
    public Guid AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Banco que reportó la cliente. "Crédito interno" si es aplicación de saldo.</summary>
    public string? Bank { get; set; }
    /// <summary>True si Bank == "Crédito interno" — el frontend lo muestra distinto.</summary>
    public bool IsInternalCredit { get; set; }
    /// <summary>Cuándo se aprobó el voucher (= cuándo entró al cierre del día).</summary>
    public DateTime DecidedAt { get; set; }
}

/// <summary>
/// Item de la sección "Anticipos retenidos por cancelación tardía".
/// La admin ve quién canceló tarde y cuánto retuvo el salón.
/// </summary>
public class ForfeitedItem
{
    public Guid VoucherId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Cuándo era la cita cancelada (display).</summary>
    public DateTime AppointmentStartAt { get; set; }
    /// <summary>Cuándo se canceló la cita (= cuándo se decidió Forfeited).</summary>
    public DateTime CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
