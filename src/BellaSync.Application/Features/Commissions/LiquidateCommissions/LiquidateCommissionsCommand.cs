using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Commissions.Dtos;

namespace BellaSync.Application.Features.Commissions.LiquidateCommissions;

/// <summary>
/// Crea un CommissionPayout — la admin marca que le pagó al estilista
/// X cierto monto cubriendo el período del From al To.
/// </summary>
public sealed record LiquidateCommissionsCommand(
    Guid StylistId,
    decimal Amount,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string? Notes,
    Guid? PaidByUserId
) : ICommand<CommissionPayoutResponse>;
