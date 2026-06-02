using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Auth.Dtos;
using BellaSync.Application.Features.Auth.Shared;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Features.Auth.Login;

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly IClock _clock;

    public LoginHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        AuthTokenIssuer tokenIssuer,
        IClock clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _clock = clock;
    }

    public async Task<Result<AuthResponse>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.IsActive)
        {
            return ApplicationError.Unauthorized("auth.invalid_credentials", "Credenciales inválidas.");
        }

        if (user.Tenant is { IsActive: false })
        {
            return ApplicationError.Unauthorized(
                "auth.tenant_inactive",
                "El salón asociado a este usuario ha sido desactivado. Contacta al soporte.");
        }

        if (!_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return ApplicationError.Unauthorized("auth.invalid_credentials", "Credenciales inválidas.");
        }

        // Método verbal: la entidad protege la mutación de LastLoginAt.
        user.MarkLogin(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        var response = await _tokenIssuer.IssueAsync(
            user: user,
            tenant: user.Tenant,
            replacesTokenHash: null,
            createdByIp: command.CreatedByIp,
            ct: ct);

        return Result<AuthResponse>.Success(response);
    }
}
