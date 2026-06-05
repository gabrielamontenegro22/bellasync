using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Services.CreateService;
using BellaSync.Application.Features.Services.DeleteService;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Application.Features.Services.GetService;
using BellaSync.Application.Features.Services.ListServices;
using BellaSync.Application.Features.Services.UpdateService;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD del catálogo de servicios del salón. Controller delgado: delega
/// toda la lógica a handlers de Application (Commands/Queries).
///
/// Responsabilidad del controller:
///   1. Validar input con FluentValidation
///   2. Despachar al handler correcto
///   3. Traducir Result&lt;T&gt; → IActionResult vía ResultActionExtensions
///
/// Lectura abierta a SalonAdmin + Receptionist. Escritura solo SalonAdmin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class ServicesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListServicesQuery, IReadOnlyList<ServiceResponse>> handler,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(new ListServicesQuery(includeInactive), ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetServiceQuery, ServiceResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetServiceQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [RequireReceptionPermission(Perm.CanEditServices)]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request,
        [FromServices] IValidator<CreateServiceRequest> validator,
        [FromServices] ICommandHandler<CreateServiceCommand, ServiceResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new CreateServiceCommand(
            request.Name,
            request.Description,
            request.Category,
            request.DurationMinutes,
            request.Price,
            request.CommissionPercentage,
            request.Color,
            request.RequiresDeposit,
            request.DepositPercentage);

        var result = await handler.HandleAsync(command, ct);
        return result.ToCreatedAtAction(nameof(GetById), v => new { id = v.Id });
    }

    [HttpPut("{id:guid}")]
    [RequireReceptionPermission(Perm.CanEditServices)]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceRequest request,
        [FromServices] IValidator<UpdateServiceRequest> validator,
        [FromServices] ICommandHandler<UpdateServiceCommand, ServiceResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new UpdateServiceCommand(
            id,
            request.Name,
            request.Description,
            request.Category,
            request.DurationMinutes,
            request.Price,
            request.CommissionPercentage,
            request.Color,
            request.RequiresDeposit,
            request.DepositPercentage,
            request.IsActive);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [RequireReceptionPermission(Perm.CanEditServices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteServiceCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteServiceCommand(id), ct);
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
