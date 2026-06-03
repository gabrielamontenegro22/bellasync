using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Pagination;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Customers.CreateCustomer;
using BellaSync.Application.Features.Customers.DeleteCustomer;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Application.Features.Customers.GetCustomer;
using BellaSync.Application.Features.Customers.GetCustomerAppointments;
using BellaSync.Application.Features.Customers.ListCustomers;
using BellaSync.Application.Features.Customers.UpdateCustomer;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRM de clientes del salón. Controller delgado: delega a handlers.
/// CRUD completo abierto a SalonAdmin y Receptionist.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class CustomersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListCustomersQuery, PaginatedResponse<CustomerResponse>> handler,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(
            new ListCustomersQuery(search, page, pageSize, includeInactive), ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetCustomerQuery, CustomerResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetCustomerQuery(id), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Historial completo de citas del cliente (pasadas + futuras), ordenado
    /// de más reciente a más antiguo. Usado por el tab "Historial" del CRM.
    /// </summary>
    [HttpGet("{id:guid}/appointments")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointments(
        Guid id,
        [FromServices] IQueryHandler<GetCustomerAppointmentsQuery, IReadOnlyList<AppointmentResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetCustomerAppointmentsQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        [FromServices] IValidator<CreateCustomerRequest> validator,
        [FromServices] ICommandHandler<CreateCustomerCommand, CustomerResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new CreateCustomerCommand(
            request.FullName,
            request.Phone,
            request.Email,
            request.Birthday,
            request.DocumentNumber,
            request.Address,
            request.Notes,
            request.AcceptsMarketing);

        var result = await handler.HandleAsync(command, ct);
        return result.ToCreatedAtAction(nameof(GetById), v => new { id = v.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        [FromServices] IValidator<UpdateCustomerRequest> validator,
        [FromServices] ICommandHandler<UpdateCustomerCommand, CustomerResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var command = new UpdateCustomerCommand(
            id,
            request.FullName,
            request.Phone,
            request.Email,
            request.Birthday,
            request.DocumentNumber,
            request.Address,
            request.Notes,
            request.AcceptsMarketing,
            request.IsActive);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteCustomerCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteCustomerCommand(id), ct);
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
