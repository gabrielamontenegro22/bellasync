using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Stylists.TimeOff.RemoveStylistTimeOff;

public sealed class RemoveStylistTimeOffHandler
    : ICommandHandler<RemoveStylistTimeOffCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<RemoveStylistTimeOffHandler> _logger;

    public RemoveStylistTimeOffHandler(
        IApplicationDbContext db,
        ILogger<RemoveStylistTimeOffHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        RemoveStylistTimeOffCommand command, CancellationToken ct)
    {
        var timeOff = await _db.StylistTimeOffs
            .FirstOrDefaultAsync(t => t.Id == command.TimeOffId, ct);
        if (timeOff is null)
            return ApplicationError.NotFound(
                "stylist.time_off_not_found", "Período no encontrado.");

        _db.StylistTimeOffs.Remove(timeOff);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("TimeOff {TimeOffId} eliminado", command.TimeOffId);
        return Result.Success();
    }
}
