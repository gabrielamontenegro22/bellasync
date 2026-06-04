using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Users.Dtos;

namespace BellaSync.Application.Features.Users.ListUsers;

/// <summary>Lista todos los usuarios del salón actual (incluye archivados).</summary>
public sealed record ListUsersQuery() : IQuery<IReadOnlyList<UserResponse>>;
