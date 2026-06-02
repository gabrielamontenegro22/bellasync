using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD del catálogo de servicios del salón.
/// Lectura abierta a SalonAdmin y Receptionist (la recepción necesita ver
/// el catálogo al agendar). Escritura solo SalonAdmin (catálogo es
/// configuración del salón).
/// El TenantId se toma del JWT — el filtro global multi-tenant
/// garantiza que cada salón solo opere sobre sus propios servicios.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class ServicesController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IValidator<CreateServiceRequest> _createValidator;
    private readonly IValidator<UpdateServiceRequest> _updateValidator;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IValidator<CreateServiceRequest> createValidator,
        IValidator<UpdateServiceRequest> updateValidator,
        ILogger<ServicesController> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <summary>
    /// Lista los servicios del salón.
    /// Por defecto solo devuelve los activos. Pasa includeInactive=true para incluir archivados.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = _db.Services.AsNoTracking();

        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        var services = await query
            .OrderBy(s => s.Name)
            .Select(s => MapToResponse(s))
            .ToListAsync(ct);

        return Ok(services);
    }

    /// <summary>Devuelve el detalle de un servicio por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return service is null ? NotFound() : Ok(MapToResponse(service));
    }

    /// <summary>Crea un nuevo servicio en el catálogo del salón actual.</summary>
    [HttpPost]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var name = request.Name.Trim();

        var nameTaken = await _db.Services
            .AnyAsync(s => s.IsActive && s.Name == name, ct);
        if (nameTaken)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Nombre duplicado",
                Detail = $"Ya existe un servicio activo con el nombre \"{name}\".",
                Status = StatusCodes.Status409Conflict
            });
        }

        // Factory del dominio: valida invariantes (nombre no vacío, duración > 0,
        // coherencia entre RequiresDeposit y DepositPercentage). El TenantId
        // lo seteamos explícitamente; el SaveChangesAsync lo confirmaría de
        // todos modos vía auto-set como red de seguridad.
        var deposit = request.RequiresDeposit
            ? Percentage.Create(request.DepositPercentage)
            : Percentage.Zero;

        var service = Service.Create(
            tenantId: _currentTenant.TenantId,
            name: name,
            category: request.Category,
            durationMinutes: request.DurationMinutes,
            price: Money.Create(request.Price),
            commission: Percentage.Create(request.CommissionPercentage),
            description: request.Description,
            color: request.Color,
            requiresDeposit: request.RequiresDeposit,
            depositPercentage: deposit);

        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceName} ({ServiceId}) creado en tenant {TenantId}",
            service.Name, service.Id, service.TenantId);

        return CreatedAtAction(nameof(GetById), new { id = service.Id }, MapToResponse(service));
    }

    /// <summary>Edita un servicio existente. Permite reactivar servicios archivados.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service is null) return NotFound();

        var newName = request.Name.Trim();

        // Si va a quedar activo y el nombre cambió, validamos colisión con otros activos
        var willBeActive = request.IsActive;
        var nameChanged = !string.Equals(service.Name, newName, StringComparison.OrdinalIgnoreCase);

        if (willBeActive && nameChanged)
        {
            var nameTaken = await _db.Services
                .AnyAsync(s => s.Id != id && s.IsActive && s.Name == newName, ct);
            if (nameTaken)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Nombre duplicado",
                    Detail = $"Ya existe otro servicio activo con el nombre \"{newName}\".",
                    Status = StatusCodes.Status409Conflict
                });
            }
        }

        // Mutación vía métodos verbales: cada uno valida sus invariantes
        // (Rename rechaza vacío, EnableDeposit rechaza percentage 0, etc.).
        service.Rename(newName);
        service.UpdateDescription(request.Description);
        service.Recategorize(request.Category);
        service.UpdateDuration(request.DurationMinutes);
        service.UpdatePricing(
            Money.Create(request.Price),
            Percentage.Create(request.CommissionPercentage));
        service.UpdateColor(request.Color);

        if (request.RequiresDeposit)
            service.EnableDeposit(Percentage.Create(request.DepositPercentage));
        else
            service.DisableDeposit();

        if (request.IsActive) service.Reactivate();
        else service.Archive();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceId} actualizado en tenant {TenantId}",
            service.Id, service.TenantId);

        return Ok(MapToResponse(service));
    }

    /// <summary>
    /// Borrado lógico: marca el servicio como inactivo.
    /// Las citas históricas siguen referenciándolo correctamente.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (service is null) return NotFound();

        if (!service.IsActive)
        {
            // Idempotente — ya estaba archivado.
            return NoContent();
        }

        service.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceId} archivado (soft delete) en tenant {TenantId}",
            service.Id, service.TenantId);

        return NoContent();
    }

    private static ServiceResponse MapToResponse(Service s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        Category = s.Category.ToString(),
        DurationMinutes = s.DurationMinutes,
        // Desempaquetar los VOs a primitivos para el contrato JSON.
        Price = s.Price.Amount,
        CommissionPercentage = s.CommissionPercentage.Value,
        Color = s.Color,
        IsActive = s.IsActive,
        RequiresDeposit = s.RequiresDeposit,
        DepositPercentage = s.DepositPercentage.Value,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary BuildModelState(
        FluentValidation.Results.ValidationResult result)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
