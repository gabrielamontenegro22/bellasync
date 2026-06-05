using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Inventory.ArchiveProduct;

public sealed record ArchiveProductCommand(Guid Id, bool Active) : ICommand;
