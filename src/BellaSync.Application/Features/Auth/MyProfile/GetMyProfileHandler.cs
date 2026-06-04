using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Auth.MyProfile;

public sealed class GetMyProfileHandler : IQueryHandler<GetMyProfileQuery, MyProfileResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<MyProfileResponse>> HandleAsync(
        GetMyProfileQuery query, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return ApplicationError.Unauthorized(
                "auth.not_authenticated",
                "No hay sesión activa.");

        // IgnoreQueryFilters porque para SuperAdmin el filtro multi-tenant
        // bloquearía el query (su TenantId es Guid.Empty). Usamos el UserId
        // del JWT — es seguro porque cada user solo lee su propio perfil.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);

        if (user is null)
            return ApplicationError.NotFound("user.not_found", "Usuario no encontrado.");

        return Result<MyProfileResponse>.Success(new MyProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            TenantName = user.Tenant?.Name,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        });
    }
}
