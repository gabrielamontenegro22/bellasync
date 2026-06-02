using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.CreatePublicAppointment;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Portal público anónimo: el cliente agenda directamente desde la web del salón.
/// El TenantSlug viene en la URL (ej. /api/PublicBooking/bella-spa).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PublicBookingController : ControllerBase
{
    [HttpPost("{tenantSlug}")]
    [ProducesResponseType(typeof(PublicBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Book(
        string tenantSlug,
        [FromBody] PublicBookingRequest request,
        [FromServices] IValidator<CreatePublicAppointmentCommand> validator,
        [FromServices] ICommandHandler<CreatePublicAppointmentCommand, PublicBookingResponse> handler,
        CancellationToken ct)
    {
        var command = new CreatePublicAppointmentCommand(
            TenantSlug: tenantSlug,
            StylistId: request.StylistId,
            ServiceId: request.ServiceId,
            StartAtUtc: request.StartAtUtc,
            ClientName: request.ClientName,
            ClientPhone: request.ClientPhone,
            ClientEmail: request.ClientEmail);

        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>Body del POST público (no incluye tenantSlug, viene en URL).</summary>
    public sealed record PublicBookingRequest(
        Guid StylistId,
        Guid ServiceId,
        DateTime StartAtUtc,
        string ClientName,
        string ClientPhone,
        string? ClientEmail);

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
