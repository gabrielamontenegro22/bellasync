using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Auth.Dtos;

namespace BellaSync.Application.Features.Auth.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? CreatedByIp) : ICommand<AuthResponse>;
