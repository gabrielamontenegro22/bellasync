using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand;
