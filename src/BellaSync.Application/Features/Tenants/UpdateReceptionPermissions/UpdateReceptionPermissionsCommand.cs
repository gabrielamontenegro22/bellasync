using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdateReceptionPermissions;

/// <summary>
/// Actualiza los permisos de recepción del tenant actual.
/// Solo la admin debería poder mandar este comando — el endpoint usa
/// [Authorize(Roles="SalonAdmin")] como guard duro.
/// </summary>
public sealed record UpdateReceptionPermissionsCommand(
    decimal? ExpenseCapCop,
    bool CanCancelWithMoney,
    bool CanCloseCash
) : ICommand<ReceptionPermissionsResponse>;
