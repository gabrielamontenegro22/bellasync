using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Stylists.DeleteStylist;

public sealed record DeleteStylistCommand(Guid Id) : ICommand;
