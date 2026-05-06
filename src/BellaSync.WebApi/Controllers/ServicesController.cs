using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Services.Dtos;
using BellaSync.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD del catálogo de servicios del salón.
/// Todos los endpoints requieren autenticación con rol SalonAdmin.
/// El TenantId se toma del JWT — el filtro global multi-tenant
/// garantiza que cada salón solo opere sobre sus propios servicios.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin")]
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

        var service = new Service
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            Name = name,
            Description = request.Description?.Trim(),
            Category = request.Category,
            DurationMinutes = request.DurationMinutes,
            Price = request.Price,
            CommissionPercentage = request.CommissionPercentage,
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Servicio {ServiceName} ({ServiceId}) creado en tenant {TenantId}",
            service.Name, service.Id, service.TenantId);

        return CreatedAtAction(nameof(GetById), new { id = service.Id }, MapToResponse(service));
    }

    /// <summary>Edita un servicio existente. Permite reactivar servicios archivados.</summary>
    [HttpPut("{id:guid}")]
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

        service.Name = newName;
        service.Description = request.Description?.Trim();
        service.Category = request.Category;
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.CommissionPercentage = request.CommissionPercentage;
        service.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        service.IsActive = request.IsActive;

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

        service.IsActive = false;
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
        Price = s.Price,
        CommissionPercentage = s.CommissionPercentage,
        Color = s.Color,
        IsActive = s.IsActive,
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
