using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.Dtos;

namespace BellaSync.Application.Features.Inventory.GetInventorySummary;

public sealed record GetInventorySummaryQuery() : IQuery<InventorySummaryResponse>;
