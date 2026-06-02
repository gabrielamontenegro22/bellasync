using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Application.Features.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Services.GetService;

public sealed class GetServiceHandler : IQueryHandler<GetServiceQuery, ServiceResponse>
{
    private readonly IApplicationDbContext _db;

    public GetServiceHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ServiceResponse>> HandleAsync(GetServiceQuery query, CancellationToken ct)
    {
        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.Id, ct);

        if (service is null)
        {
            return ApplicationError.NotFound(
                "service.not_found",
                $"No existe un servicio con id {query.Id}.");
        }

        return Result<ServiceResponse>.Success(ServiceMapper.ToResponse(service));
    }
}
