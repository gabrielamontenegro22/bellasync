using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.CreateStylist;
using BellaSync.Application.Features.Stylists.DeleteStylist;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.GetStylist;
using BellaSync.Application.Features.Stylists.ListStylists;
using BellaSync.Application.Features.Stylists.TimeOff.AddStylistTimeOff;
using BellaSync.Application.Features.Stylists.TimeOff.Dtos;
using BellaSync.Application.Features.Stylists.TimeOff.GetAffectedAppointments;
using BellaSync.Application.Features.Stylists.TimeOff.ListStylistTimeOffs;
using BellaSync.Application.Features.Stylists.TimeOff.RemoveStylistTimeOff;
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
    [RequireReceptionPermission(Perm.CanEditStylists)]
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
    [RequireReceptionPermission(Perm.CanEditStylists)]
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
    [RequireReceptionPermission(Perm.CanEditStylists)]
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

    // ============================================================
    // Días libres / vacaciones de estilistas
    // ============================================================

    /// <summary>
    /// GET /api/Stylists/{id}/time-off
    /// Lista los períodos de vacaciones/días libres del estilista
    /// (últimos 90 días + futuros).
    /// </summary>
    [HttpGet("{id:guid}/time-off")]
    [ProducesResponseType(typeof(IReadOnlyList<StylistTimeOffResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTimeOff(
        Guid id,
        [FromServices] IQueryHandler<ListStylistTimeOffsQuery, IReadOnlyList<StylistTimeOffResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListStylistTimeOffsQuery(id), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// POST /api/Stylists/{id}/time-off
    /// Marca un período (rango de días) como no disponible. Solo SalonAdmin.
    /// </summary>
    [HttpPost("{id:guid}/time-off")]
    [RequireReceptionPermission(Perm.CanEditStylists)]
    [ProducesResponseType(typeof(StylistTimeOffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTimeOff(
        Guid id,
        [FromBody] AddTimeOffRequest request,
        [FromServices] ICommandHandler<AddStylistTimeOffCommand, StylistTimeOffResponse> handler,
        CancellationToken ct)
    {
        var command = new AddStylistTimeOffCommand(
            id, request.FromDate, request.ToDate, request.Reason);
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// DELETE /api/Stylists/time-off/{timeOffId}
    /// Borra un período. Solo SalonAdmin. La cita ya agendada NO se
    /// reagenda automáticamente — la admin tiene la lista en pantalla.
    /// </summary>
    [HttpDelete("time-off/{timeOffId:guid}")]
    [RequireReceptionPermission(Perm.CanEditStylists)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimeOff(
        Guid timeOffId,
        [FromServices] ICommandHandler<RemoveStylistTimeOffCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RemoveStylistTimeOffCommand(timeOffId), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Stylists/{id}/affected-appointments?from=YYYY-MM-DD&amp;to=YYYY-MM-DD
    /// Preview de citas que requerirían reagendarse si se marca el rango.
    /// Sirve tanto antes de confirmar (preview) como después (review).
    /// </summary>
    [HttpGet("{id:guid}/affected-appointments")]
    [ProducesResponseType(typeof(IReadOnlyList<AffectedAppointmentRow>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAffected(
        Guid id,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromServices] IQueryHandler<GetAffectedAppointmentsQuery, IReadOnlyList<AffectedAppointmentRow>> handler,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var fromDate)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var toDate))
            return BadRequest(new { error = "Fechas inválidas (YYYY-MM-DD)." });

        var result = await handler.HandleAsync(
            new GetAffectedAppointmentsQuery(id, fromDate, toDate), ct);
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

public sealed class AddTimeOffRequest
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Reason { get; set; }
}
