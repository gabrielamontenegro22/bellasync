using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Appointments.CancelAppointment;
using BellaSync.Application.Features.Appointments.CompleteAppointment;
using BellaSync.Application.Features.Appointments.ConfirmAppointment;
using BellaSync.Application.Features.Appointments.CreateAppointment;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.Appointments.GetAgenda;
using BellaSync.Application.Features.Appointments.GetAppointment;
using BellaSync.Application.Features.Appointments.MarkInProgress;
using BellaSync.Application.Features.Appointments.MarkNoShow;
using BellaSync.Application.Features.Appointments.RescheduleAppointment;
using BellaSync.Application.Features.Payments.Dtos;
using BellaSync.Application.Features.Payments.RegisterPayment;
using BellaSync.Domain.Entities;
using System.Security.Claims;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD de citas (recepción autenticada).
/// SalonAdmin + Receptionist pueden todo el ciclo de vida.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class AppointmentsController : ControllerBase
{
    /// <summary>Agenda del día. ?date=YYYY-MM-DD, ?stylistId=guid opcional.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AgendaResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgenda(
        [FromServices] IQueryHandler<GetAgendaQuery, AgendaResponse> handler,
        [FromQuery] DateOnly? date = null,
        [FromQuery] Guid? stylistId = null,
        CancellationToken ct = default)
    {
        var theDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await handler.HandleAsync(new GetAgendaQuery(theDate, stylistId), ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetAppointmentQuery, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetAppointmentQuery(id), ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentCommand command,
        [FromServices] IValidator<CreateAppointmentCommand> validator,
        [FromServices] ICommandHandler<CreateAppointmentCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        // BypassAdvanceWindow es privilegio de SalonAdmin (walk-ins).
        // Si lo manda un Receptionist, lo silenciamos a false antes del handler.
        if (command.BypassAdvanceWindow && !User.IsInRole("SalonAdmin"))
        {
            command = command with { BypassAdvanceWindow = false };
        }

        var result = await handler.HandleAsync(command, ct);
        return result.ToCreatedAtAction(nameof(GetById), v => new { id = v.Id });
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(
        Guid id,
        [FromServices] ICommandHandler<ConfirmAppointmentCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ConfirmAppointmentCommand(id), ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelAppointmentRequest? request,
        [FromServices] ICommandHandler<CancelAppointmentCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new CancelAppointmentCommand(id, request?.Reason), ct);
        return result.ToActionResult();
    }

    public sealed record CancelAppointmentRequest(string? Reason);

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(
        Guid id,
        [FromServices] ICommandHandler<MarkInProgressCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new MarkInProgressCommand(id), ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromServices] ICommandHandler<CompleteAppointmentCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CompleteAppointmentCommand(id), ct);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/no-show")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NoShow(
        Guid id,
        [FromServices] ICommandHandler<MarkNoShowCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new MarkNoShowCommand(id), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reagenda una cita Pending/Confirmed a un nuevo horario. Mismo
    /// stylist/service/customer — solo cambia el slot. Valida overlap
    /// excluyendo la propia cita.
    /// </summary>
    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        Guid id,
        [FromBody] RescheduleAppointmentRequest request,
        [FromServices] ICommandHandler<RescheduleAppointmentCommand, AppointmentResponse> handler,
        CancellationToken ct)
    {
        // BypassAdvanceWindow es privilegio de SalonAdmin — al igual que en Create,
        // si lo manda un Receptionist lo silenciamos antes de pasar al handler.
        var bypass = request.BypassAdvanceWindow && User.IsInRole("SalonAdmin");
        var command = new RescheduleAppointmentCommand(id, request.NewStartAtUtc, bypass);
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    public sealed record RescheduleAppointmentRequest(
        DateTime NewStartAtUtc,
        bool BypassAdvanceWindow = false);

    /// <summary>
    /// Registra un pago recibido por esta cita (efectivo, transferencia,
    /// tarjeta). NO procesa el pago — solo lo deja anotado.
    /// La cita debe estar InProgress o Completed.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterPayment(
        Guid id,
        [FromBody] RegisterPaymentRequest request,
        [FromServices] ICommandHandler<RegisterPaymentCommand, PaymentResponse> handler,
        CancellationToken ct)
    {
        // El UserId del JWT viene en el claim "sub" (estándar OIDC) o en
        // NameIdentifier dependiendo de cómo configuremos. Tomamos cualquiera.
        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var parsed)) userId = parsed;

        var command = new RegisterPaymentCommand(
            AppointmentId: id,
            Method: request.Method,
            Amount: request.Amount,
            Tip: request.Tip,
            Reference: request.Reference,
            RegisteredByUserId: userId);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    public sealed record RegisterPaymentRequest(
        PaymentMethod Method,
        decimal Amount,
        decimal Tip,
        string? Reference);

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
