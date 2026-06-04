using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Appointments.CreatePublicAppointment;
using BellaSync.Application.Features.Appointments.Dtos;
using BellaSync.Application.Features.PublicCatalog.Dtos;
using BellaSync.Application.Features.PublicCatalog.ListPublicServices;
using BellaSync.Application.Features.PublicCatalog.ListPublicStylists;
using BellaSync.Application.Features.Vouchers.CreateVoucher;
using BellaSync.Application.Features.Vouchers.Dtos;
using BellaSync.Domain.Entities;
using BellaSync.WebApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Portal público anónimo: el cliente agenda directamente desde la web del salón.
/// El TenantSlug viene en la URL (ej. /api/PublicBooking/bella-spa).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PublicBookingController : ControllerBase
{
    [HttpGet("{tenantSlug}/services")]
    [ProducesResponseType(typeof(IEnumerable<PublicServiceItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(
        string tenantSlug,
        [FromServices] IQueryHandler<ListPublicServicesQuery, IReadOnlyList<PublicServiceItem>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListPublicServicesQuery(tenantSlug), ct);
        return result.ToActionResult();
    }

    [HttpGet("{tenantSlug}/stylists")]
    [ProducesResponseType(typeof(IEnumerable<PublicStylistItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStylists(
        string tenantSlug,
        [FromServices] IQueryHandler<ListPublicStylistsQuery, IReadOnlyList<PublicStylistItem>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListPublicStylistsQuery(tenantSlug), ct);
        return result.ToActionResult();
    }

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

    /// <summary>
    /// El cliente sube el comprobante bancario de su anticipo.
    /// Anonymous: viene del portal público después de agendar.
    ///
    /// Multipart/form-data: archivo "file" + campos de metadata.
    /// Validamos que la cita pertenezca al tenant del slug en la URL
    /// (defensa: si alguien intenta subir voucher para una cita de
    /// otro tenant, rechazamos).
    /// </summary>
    [HttpPost("{tenantSlug}/appointments/{appointmentId:guid}/voucher")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadRules.MaxFileSizeBytes + 1024)]
    [ProducesResponseType(typeof(VoucherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadVoucher(
        string tenantSlug,
        Guid appointmentId,
        [FromForm] PublicVoucherUploadRequest form,
        [FromServices] IApplicationDbContext db,
        [FromServices] IFileStorage storage,
        [FromServices] ICommandHandler<CreateVoucherCommand, VoucherResponse> handler,
        CancellationToken ct)
    {
        // 1. Verificar tenant + cita coinciden con el slug de la URL.
        var slug = tenantSlug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug == slug && t.IsActive)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
            return NotFound(new { error = "Salón no encontrado." });

        var appointmentExists = await db.Appointments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.Id == appointmentId && a.TenantId == tenant.Id, ct);
        if (!appointmentExists)
            return NotFound(new { error = "La cita no existe en este salón." });

        // 2. Validar archivo.
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { error = "El comprobante es obligatorio." });
        if (form.File.Length > FileUploadRules.MaxFileSizeBytes)
            return BadRequest(new { error = "El archivo supera 5 MB." });
        if (!FileUploadRules.IsAllowedImage(form.File.ContentType))
            return BadRequest(new { error = "Solo aceptamos imágenes (JPG/PNG/WebP/HEIC)." });
        if (form.ReportedAmount <= 0m)
            return BadRequest(new { error = "El monto reportado debe ser mayor a cero." });

        // 3. Guardar el archivo y obtener URL.
        string imageUrl;
        await using (var stream = form.File.OpenReadStream())
        {
            imageUrl = await storage.SaveAsync(
                category: "vouchers",
                fileName: form.File.FileName,
                contentType: form.File.ContentType,
                content: stream,
                ct: ct);
        }

        // 4. Crear voucher usando el handler existente (idempotente con
        //    create logic, persiste y devuelve el response mapeado).
        var command = new CreateVoucherCommand(
            AppointmentId: appointmentId,
            ReportedAmount: form.ReportedAmount,
            Bank: form.Bank,
            ReferenceNumber: form.ReferenceNumber,
            SenderName: form.SenderName,
            SenderPhone: form.SenderPhone,
            ImageUrl: imageUrl);

        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>Multipart form para subir voucher desde portal público.</summary>
    public sealed class PublicVoucherUploadRequest
    {
        public IFormFile? File { get; set; }
        public decimal ReportedAmount { get; set; }
        public string? Bank { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? SenderName { get; set; }
        public string? SenderPhone { get; set; }
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
