using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.CreateStylist;
using BellaSync.Application.Features.Stylists.DeleteStylist;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.GetStylist;
using BellaSync.Application.Features.Stylists.ListStylists;
using BellaSync.Application.Features.Stylists.UpdateStylist;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD de estilistas. Controller delgado: delega a handlers.
/// Lectura: SalonAdmin + Receptionist. Escritura: solo SalonAdmin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class StylistsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StylistResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListStylistsQuery, IReadOnlyList<StylistResponse>> handler,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(new ListStylistsQuery(includeInactive), ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetStylistQuery, StylistResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetStylistQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStylistRequest request,
        [FromServices] IValidator<CreateStylistRequest> validator,
        [FromServices] ICommandHandler<CreateStylistCommand, StylistResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new CreateStylistCommand(
            request.FullName,
            request.Role,
            request.Email,
            request.Phone,
            request.IdNumber,
            request.Color,
            request.HireDate,
            request.ServiceIds);

        var result = await handler.HandleAsync(command, ct);
        return result.ToCreatedAtAction(nameof(GetById), v => new { id = v.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStylistRequest request,
        [FromServices] IValidator<UpdateStylistRequest> validator,
        [FromServices] ICommandHandler<UpdateStylistCommand, StylistResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new UpdateStylistCommand(
            id,
            request.FullName,
            request.Role,
            request.Email,
            request.Phone,
            request.IdNumber,
            request.Color,
            request.HireDate,
            request.Status,
            request.ServiceIds);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteStylistCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteStylistCommand(id), ct);
        return result.ToActionResult();
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
