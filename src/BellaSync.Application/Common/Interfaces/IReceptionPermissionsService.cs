namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Lee los toggles de permisos de recepción del tenant actual.
/// Abstrae el query (1 row de tenants) para que los handlers no
/// tengan que duplicar la lógica de AsNoTracking + Where + Select.
///
/// Cachea por request (singleton scoped) — el mismo handler puede
/// chequear varios permisos sin pagar N queries.
/// </summary>
public interface IReceptionPermissionsService
{
    /// <summary>
    /// Snapshot de permisos del tenant actual. Si no hay sesión/tenant
    /// devuelve todos los flags en false (lo más restrictivo).
    /// </summary>
    Task<ReceptionPermissionsSnapshot> GetAsync(CancellationToken ct);
}

/// <summary>
/// DTO interno para evitar acoplar el handler a la entidad Tenant.
/// Espeja los 7 toggles + el cap de egresos.
/// </summary>
public sealed record ReceptionPermissionsSnapshot(
    decimal? ExpenseCapCop,
    bool CanCancelWithMoney,
    bool CanCloseCash,
    bool CanEditStylists,
    bool CanEditServices,
    bool CanViewReports,
    bool CanViewCommissions,
    bool CanEditSchedule,
    bool CanEditPaymentPolicy,
    bool CanEditSalonInfo,
    bool CanEditInventory,
    bool CanRefundDeposit);
