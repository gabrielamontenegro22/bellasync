using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Users.CreateUser;
using BellaSync.Application.Features.Users.Dtos;
using BellaSync.Application.Features.Users.ListUsers;
using BellaSync.Application.Features.Users.SetUserActive;
using BellaSync.Application.Features.Users.UpdateUser;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Gestión de usuarios del salón. Solo SalonAdmin puede crear/editar/
/// archivar usuarios. Los Receptionists ven sus propias citas pero
/// no gestionan el equipo.
///
///   GET   /api/Users                    → lista todos (activos + archivados)
///   POST  /api/Users                    → crea SalonAdmin o Receptionist
///   PUT   /api/Users/{id}               → cambia nombre + rol
///   POST  /api/Users/{id}/archive       → IsActive=false
///   POST  /api/Users/{id}/reactivate    → IsActive=true
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin")]
public class UsersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListUsersQuery, IReadOnlyList<UserResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListUsersQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserCommand command,
        [FromServices] ICommandHandler<CreateUserCommand, UserResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        [FromServices] ICommandHandler<UpdateUserCommand, UserResponse> handler,
        CancellationToken ct)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Role);
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromServices] ICommandHandler<SetUserActiveCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new SetUserActiveCommand(id, false), ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(
        Guid id,
        [FromServices] ICommandHandler<SetUserActiveCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new SetUserActiveCommand(id, true), ct);
        return result.ToActionResult();
    }
}

public sealed class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
