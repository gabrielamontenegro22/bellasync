using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Services.DeleteService;

public sealed class DeleteServiceHandler : ICommandHandler<DeleteServiceCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<DeleteServiceHandler> _logger;

    public DeleteServiceHandler(IApplicationDbContext db, ILogger<DeleteServiceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(DeleteServiceCommand command, CancellationToken ct)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (service is null)
        {
            return ApplicationError.NotFound(
                "service.not_found",
                $"No existe un servicio con id {command.Id}.");
        }

        // Idempotente: si ya estaba archivado, no hacemos nada y devolvemos éxito.
        if (!service.IsActive) return Result.Success();

        service.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceId} archivado en tenant {TenantId}",
            service.Id, service.TenantId);

        return Result.Success();
    }
}
