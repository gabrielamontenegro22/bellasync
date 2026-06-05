using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Inventory.ArchiveCategory;

public sealed record ArchiveCategoryCommand(Guid Id, bool Active) : ICommand;
