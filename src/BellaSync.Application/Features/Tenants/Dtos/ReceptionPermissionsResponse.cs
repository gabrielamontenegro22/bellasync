namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Snapshot de los permisos de recepción del salón. La admin los
/// edita desde /configuracion/permisos. Los handlers consultan estos
/// valores en tiempo real para decidir si dejan pasar una acción.
///
/// Defaults conservadores: solo expenseCapCop=$100k y canCancelWithMoney
/// llegan true en tenants nuevos. El resto es OFF.
/// </summary>
public sealed class ReceptionPermissionsResponse
{
    // ---- Operación diaria ----

    /// <summary>
    /// Tope COP de egresos sin admin. null = sin límite, 0 = bloqueado,
    /// X = cap. La admin no tiene cap NUNCA.
    /// </summary>
    public decimal? ExpenseCapCop { get; init; }

    /// <summary>Si recepción puede cancelar citas con plata (con nota).</summary>
    public bool CanCancelWithMoney { get; init; }

    /// <summary>Si recepción puede firmar el cierre de caja.</summary>
    public bool CanCloseCash { get; init; }

    /// <summary>Si recepción puede override la regla automática de devolución de anticipos.</summary>
    public bool CanRefundDeposit { get; init; }

    // ---- Catálogo del salón ----

    /// <summary>Crear/editar/borrar estilistas + marcar vacaciones.</summary>
    public bool CanEditStylists { get; init; }

    /// <summary>Crear/editar/borrar servicios y cambiar precios.</summary>
    public bool CanEditServices { get; init; }

    /// <summary>Crear/editar/archivar productos y registrar movimientos de inventario.</summary>
    public bool CanEditInventory { get; init; }

    // ---- Información sensible (KPIs financieros) ----

    /// <summary>Ver /reportes (facturación, KPIs, tendencias).</summary>
    public bool CanViewReports { get; init; }

    /// <summary>Ver /comisiones (cuánto le toca a cada estilista).</summary>
    public bool CanViewCommissions { get; init; }

    // ---- Configuración del salón ----

    /// <summary>Editar /configuracion/horario.</summary>
    public bool CanEditSchedule { get; init; }

    /// <summary>Editar /configuracion/pagos (política).</summary>
    public bool CanEditPaymentPolicy { get; init; }

    /// <summary>Editar /configuracion/general (nombre, dirección, logo).</summary>
    public bool CanEditSalonInfo { get; init; }
}
