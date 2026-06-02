using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Stylists.DeleteStylist;

public sealed class DeleteStylistHandler : ICommandHandler<DeleteStylistCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<DeleteStylistHandler> _logger;

    public DeleteStylistHandler(IApplicationDbContext db, ILogger<DeleteStylistHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(DeleteStylistCommand command, CancellationToken ct)
    {
        var stylist = await _db.Stylists.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (stylist is null)
        {
            return ApplicationError.NotFound(
                "stylist.not_found",
                $"No existe un estilista con id {command.Id}.");
        }

        if (stylist.Status == StylistStatus.Inactive) return Result.Success();

        stylist.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estilista {StylistId} archivado en tenant {TenantId}",
            stylist.Id, stylist.TenantId);

        return Result.Success();
    }
}
