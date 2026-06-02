using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword) : ICommand;
