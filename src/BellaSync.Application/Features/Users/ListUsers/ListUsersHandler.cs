using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Users.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Users.ListUsers;

public sealed class ListUsersHandler
    : IQueryHandler<ListUsersQuery, IReadOnlyList<UserResponse>>
{
    private readonly IApplicationDbContext _db;

    public ListUsersHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<UserResponse>>> HandleAsync(
        ListUsersQuery query, CancellationToken ct)
    {
        // El filtro global multi-tenant restringe automáticamente al
        // tenant del JWT. Incluimos archivados — la admin necesita verlos
        // para reactivar si fueron desactivados por error.
        var users = await _db.Users
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FullName)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<UserResponse>>.Success(users);
    }
}
