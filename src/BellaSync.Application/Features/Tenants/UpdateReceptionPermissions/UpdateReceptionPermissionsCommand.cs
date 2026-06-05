using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdateReceptionPermissions;

/// <summary>
/// Actualiza el set COMPLETO de permisos de recepción. La admin manda
/// todos los campos en cada PUT (no es PATCH) para evitar ambigüedad
/// sobre flags omitidos. AdminController exige [Authorize SalonAdmin].
/// </summary>
public sealed record UpdateReceptionPermissionsCommand(
    // Operación diaria
    decimal? ExpenseCapCop,
    bool CanCancelWithMoney,
    bool CanCloseCash,
    bool CanRefundDeposit,
    // Catálogo
    bool CanEditStylists,
    bool CanEditServices,
    bool CanEditInventory,
    // Info sensible
    bool CanViewReports,
    bool CanViewCommissions,
    // Configuración
    bool CanEditSchedule,
    bool CanEditPaymentPolicy,
    bool CanEditSalonInfo
) : ICommand<ReceptionPermissionsResponse>;
