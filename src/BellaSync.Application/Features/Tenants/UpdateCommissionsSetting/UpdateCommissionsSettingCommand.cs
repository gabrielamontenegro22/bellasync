using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdateCommissionsSetting;

/// <summary>
/// Toggle del módulo de Comisiones del salón. Idempotente. Cuando se
/// apaga, los CommissionPayout históricos NO se borran — solo se
/// esconden de la UI; si se reactiva, vuelven a verse.
/// </summary>
public sealed record UpdateCommissionsSettingCommand(bool Enabled)
    : ICommand<CommissionsSettingResponse>;
