using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Auth.Dtos;

namespace BellaSync.Application.Features.Auth.RefreshAccessToken;

public sealed record RefreshAccessTokenCommand(
    string RefreshToken,
    string? CreatedByIp) : ICommand<AuthResponse>;
