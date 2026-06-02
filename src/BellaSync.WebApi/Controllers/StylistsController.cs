using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRUD del catálogo de estilistas del salón.
/// La asignación de servicios al estilista se maneja en los mismos endpoints
/// (POST y PUT reciben ServiceIds[] que sincroniza la relación M:N).
/// Lectura abierta a SalonAdmin y Receptionist (la recepción necesita ver
/// el equipo al agendar). Escritura solo SalonAdmin (gestión del equipo es
/// responsabilidad del admin).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class StylistsController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IValidator<CreateStylistRequest> _createValidator;
    private readonly IValidator<UpdateStylistRequest> _updateValidator;
    private readonly ILogger<StylistsController> _logger;

    public StylistsController(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IValidator<CreateStylistRequest> createValidator,
        IValidator<UpdateStylistRequest> updateValidator,
        ILogger<StylistsController> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <summary>Lista los estilistas del salón. Por defecto solo activos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StylistResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service);

        IQueryable<Stylist> filtered = includeInactive
            ? query
            : query.Where(s => s.Status != StylistStatus.Inactive);

        var stylists = await filtered
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);

        return Ok(stylists.Select(MapToResponse));
    }

    /// <summary>Devuelve el detalle de un estilista por id, con sus servicios asignados.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var stylist = await _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return stylist is null ? NotFound() : Ok(MapToResponse(stylist));
    }

    /// <summary>Crea un estilista nuevo y opcionalmente le asigna servicios.</summary>
    [HttpPost]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStylistRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var fullName = request.FullName.Trim();

        var nameTaken = await _db.Stylists
            .AnyAsync(s => s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
        if (nameTaken)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Estilista duplicado",
                Detail = $"Ya existe un estilista activo con el nombre \"{fullName}\".",
                Status = StatusCodes.Status409Conflict
            });
        }

        // Validar que todos los serviceIds existan en este tenant y estén activos
        if (request.ServiceIds.Count > 0)
        {
            var validServiceIds = await _db.Services
                .Where(s => request.ServiceIds.Contains(s.Id) && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var invalidIds = request.ServiceIds.Except(validServiceIds).ToList();
            if (invalidIds.Count > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Servicios inválidos",
                    Detail = $"Los siguientes Ids no corresponden a servicios activos del salón: {string.Join(", ", invalidIds)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        // Factory del dominio con invariantes validadas (nombre + cargo obligatorios).
        var stylist = Stylist.Create(
            tenantId: _currentTenant.TenantId,
            fullName: fullName,
            role: request.Role,
            email: request.Email,
            phone: request.Phone,
            idNumber: request.IdNumber,
            color: request.Color,
            hireDate: request.HireDate);

        foreach (var serviceId in request.ServiceIds.Distinct())
        {
            stylist.StylistServices.Add(new StylistService
            {
                StylistId = stylist.Id,
                ServiceId = serviceId,
                TenantId = _currentTenant.TenantId,
                AssignedAt = DateTime.UtcNow
            });
        }

        _db.Stylists.Add(stylist);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estilista {FullName} ({StylistId}) creado en tenant {TenantId} con {ServiceCount} servicios",
            stylist.FullName, stylist.Id, stylist.TenantId, stylist.StylistServices.Count);

        // Releer con includes para devolver respuesta completa
        var created = await _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service)
            .FirstAsync(s => s.Id == stylist.Id, ct);

        return CreatedAtAction(nameof(GetById), new { id = stylist.Id }, MapToResponse(created));
    }

    /// <summary>
    /// Edita un estilista. La lista ServiceIds reemplaza completamente
    /// las asignaciones actuales (sincronización del set).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(typeof(StylistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStylistRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var stylist = await _db.Stylists
            .Include(s => s.StylistServices)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (stylist is null) return NotFound();

        var fullName = request.FullName.Trim();

        var nameChanged = !string.Equals(stylist.FullName, fullName, StringComparison.OrdinalIgnoreCase);
        if (request.Status != StylistStatus.Inactive && nameChanged)
        {
            var nameTaken = await _db.Stylists
                .AnyAsync(s => s.Id != id && s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
            if (nameTaken)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Estilista duplicado",
                    Detail = $"Ya existe otro estilista activo con el nombre \"{fullName}\".",
                    Status = StatusCodes.Status409Conflict
                });
            }
        }

        // Validar serviceIds nuevos
        var requestedServiceIds = request.ServiceIds.Distinct().ToList();
        if (requestedServiceIds.Count > 0)
        {
            var validIds = await _db.Services
                .Where(s => requestedServiceIds.Contains(s.Id) && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var invalidIds = requestedServiceIds.Except(validIds).ToList();
            if (invalidIds.Count > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Servicios inválidos",
                    Detail = $"Los siguientes Ids no corresponden a servicios activos del salón: {string.Join(", ", invalidIds)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        // Aplicar cambios escalares via métodos verbales.
        stylist.Rename(fullName);
        stylist.ChangeRole(request.Role);
        stylist.UpdateContact(request.Email, request.Phone, request.IdNumber);
        stylist.UpdateColor(request.Color);
        stylist.SetHireDate(request.HireDate);

        // Transición de estado: cada método encapsula la regla.
        switch (request.Status)
        {
            case StylistStatus.Active: stylist.Reactivate(); break;
            case StylistStatus.Vacation: stylist.GoOnVacation(); break;
            case StylistStatus.Inactive: stylist.Archive(); break;
        }

        // Sincronizar relación M:N con servicios
        var currentServiceIds = stylist.StylistServices.Select(ss => ss.ServiceId).ToHashSet();
        var requestedSet = requestedServiceIds.ToHashSet();

        // Quitar las que ya no están
        var toRemove = stylist.StylistServices
            .Where(ss => !requestedSet.Contains(ss.ServiceId))
            .ToList();
        foreach (var ss in toRemove) _db.StylistServices.Remove(ss);

        // Agregar las nuevas
        var toAdd = requestedSet.Except(currentServiceIds);
        foreach (var newServiceId in toAdd)
        {
            stylist.StylistServices.Add(new StylistService
            {
                StylistId = stylist.Id,
                ServiceId = newServiceId,
                TenantId = _currentTenant.TenantId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estilista {StylistId} actualizado en tenant {TenantId}. Servicios: {Count}",
            stylist.Id, stylist.TenantId, requestedSet.Count);

        var updated = await _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service)
            .FirstAsync(s => s.Id == stylist.Id, ct);

        return Ok(MapToResponse(updated));
    }

    /// <summary>Borrado lógico: marca el estilista como inactivo.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SalonAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var stylist = await _db.Stylists.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (stylist is null) return NotFound();

        if (stylist.Status == StylistStatus.Inactive) return NoContent();

        stylist.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estilista {StylistId} archivado en tenant {TenantId}",
            stylist.Id, stylist.TenantId);

        return NoContent();
    }

    private static StylistResponse MapToResponse(Stylist s) => new()
    {
        Id = s.Id,
        FullName = s.FullName,
        Role = s.Role,
        Email = s.Email,
        Phone = s.Phone,
        IdNumber = s.IdNumber,
        Color = s.Color,
        HireDate = s.HireDate,
        Status = s.Status.ToString(),
        UserId = s.UserId,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        Services = s.StylistServices
            .Where(ss => ss.Service is not null)
            .Select(ss => new StylistAssignedServiceDto
            {
                Id = ss.Service!.Id,
                Name = ss.Service.Name,
                Category = ss.Service.Category.ToString(),
                DurationMinutes = ss.Service.DurationMinutes,
                Price = ss.Service.Price.Amount
            })
            .OrderBy(x => x.Name)
            .ToList()
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
