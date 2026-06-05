namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Snapshot de los permisos de recepción del salón.
/// La admin lo lee/edita desde /configuracion/permisos.
/// Los handlers de operación (RegisterExpense, CancelAppointment,
/// CreateCashClosing) consultan estos valores en tiempo real.
/// </summary>
public sealed class ReceptionPermissionsResponse
{
    /// <summary>
    /// Tope en COP de egresos que recepción puede registrar sin admin.
    /// null = sin límite. 0 = recepción no puede registrar.
    /// X &gt; 0 = cap específico.
    /// </summary>
    public decimal? ExpenseCapCop { get; init; }

    /// <summary>
    /// Si recepción puede cancelar citas con plata asociada.
    /// Cuando es true, el frontend exige razón obligatoria.
    /// </summary>
    public bool CanCancelWithMoney { get; init; }

    /// <summary>Si recepción puede firmar el cierre de caja del día.</summary>
    public bool CanCloseCash { get; init; }
}
