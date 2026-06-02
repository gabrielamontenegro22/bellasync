using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Auth.Dtos;

namespace BellaSync.Application.Features.Auth.RegisterSalon;

/// <summary>
/// Comando para crear un Tenant + User admin en una operación atómica.
/// CreatedByIp es opcional (lo setea el controller desde HttpContext;
/// no viaja en el body del request).
/// </summary>
public sealed record RegisterSalonCommand(
    string SalonName,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword,
    string? CreatedByIp) : ICommand<AuthResponse>;
