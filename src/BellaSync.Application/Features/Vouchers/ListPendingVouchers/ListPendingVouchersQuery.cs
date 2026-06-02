using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Vouchers.Dtos;

namespace BellaSync.Application.Features.Vouchers.ListPendingVouchers;

/// <summary>Lista vouchers en estado Pending del tenant actual, ordenados por urgencia.</summary>
public sealed record ListPendingVouchersQuery : IQuery<IReadOnlyList<VoucherResponse>>;
