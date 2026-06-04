using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Users.Dtos;

namespace BellaSync.Application.Features.Users.UpdateUser;

/// <summary>
/// Actualiza nombre y/o rol de un user existente. NO toca password
/// (eso lo hace cada user por ForgotPassword). NO toca email
/// (cambiar identificador es flujo distinto, no soportado por ahora).
///
/// Restricciones (handler):
///   - No se puede demoter al último SalonAdmin activo del tenant.
///   - No se puede cambiar a SuperAdmin (rol cross-tenant, no
///     desde el panel del salón).
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Role) : ICommand<UserResponse>;
