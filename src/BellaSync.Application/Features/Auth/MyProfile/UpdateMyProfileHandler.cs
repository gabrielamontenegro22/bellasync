using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Auth.MyProfile;

public sealed class UpdateMyProfileHandler : ICommandHandler<UpdateMyProfileCommand, MyProfileResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateMyProfileHandler> _logger;

    public UpdateMyProfileHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<UpdateMyProfileHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<MyProfileResponse>> HandleAsync(
        UpdateMyProfileCommand command, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return ApplicationError.Unauthorized(
                "auth.not_authenticated",
                "No hay sesión activa.");

        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);

        if (user is null)
            return ApplicationError.NotFound("user.not_found", "Usuario no encontrado.");

        try
        {
            user.Rename(command.FullName);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("user.invalid", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Perfil propio actualizado por user {UserId}.", user.Id);

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
