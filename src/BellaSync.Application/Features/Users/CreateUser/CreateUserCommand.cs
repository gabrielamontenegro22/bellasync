using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Users.Dtos;

namespace BellaSync.Application.Features.Users.CreateUser;

/// <summary>
/// La admin del salón crea un usuario adicional (típicamente Receptionist,
/// también podría ser otro SalonAdmin para tener 2 dueños).
/// El password lo elige la admin y lo comunica al user — en una iteración
/// futura, mejor: mandar email con invitación + link de reset.
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    /// <summary>"SalonAdmin" | "Receptionist".</summary>
    string Role) : ICommand<UserResponse>;
