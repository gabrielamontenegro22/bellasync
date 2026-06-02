using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Stylists.GetStylist;

public sealed class GetStylistHandler : IQueryHandler<GetStylistQuery, StylistResponse>
{
    private readonly IApplicationDbContext _db;

    public GetStylistHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<StylistResponse>> HandleAsync(GetStylistQuery query, CancellationToken ct)
    {
        var stylist = await _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service)
            .FirstOrDefaultAsync(s => s.Id == query.Id, ct);

        if (stylist is null)
        {
            return ApplicationError.NotFound(
                "stylist.not_found",
                $"No existe un estilista con id {query.Id}.");
        }

        return Result<StylistResponse>.Success(StylistMapper.ToResponse(stylist));
    }
}
