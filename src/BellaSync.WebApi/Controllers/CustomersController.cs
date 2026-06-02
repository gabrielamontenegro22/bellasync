using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Pagination;
using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// CRM de clientes del salón.
/// Listado paginado con búsqueda por nombre/teléfono.
/// CRUD completo abierto a SalonAdmin y Receptionist — la recepción
/// gestiona los clientes en el día a día.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class CustomersController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator,
        ILogger<CustomersController> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <summary>
    /// Lista los clientes del salón con paginación y búsqueda.
    /// El parámetro `search` filtra por nombre o teléfono (substring, case-insensitive).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Customers.AsNoTracking();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.FullName, term) ||
                EF.Functions.ILike(c.Phone, term));
        }

        var totalItems = await query.CountAsync(ct);

        var customers = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToResponse(c))
            .ToListAsync(ct);

        return Ok(PaginatedResponse<CustomerResponse>.Create(customers, page, pageSize, totalItems));
    }

    /// <summary>Devuelve el detalle de un cliente por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return customer is null ? NotFound() : Ok(MapToResponse(customer));
    }

    /// <summary>Crea un cliente nuevo.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var phone = NormalizePhone(request.Phone);

        // Chequear duplicado por teléfono entre activos
        var existing = await _db.Customers
            .Where(c => c.IsActive && c.Phone == phone)
            .Select(c => new { c.Id, c.FullName })
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Cliente duplicado",
                Detail = $"Ya existe un cliente activo con el teléfono {phone}: \"{existing.FullName}\" (id: {existing.Id}).",
                Status = StatusCodes.Status409Conflict
            });
        }

        // Factory del dominio con invariantes validadas (nombre + phone obligatorios).
        var customer = Customer.Create(
            tenantId: _currentTenant.TenantId,
            fullName: request.FullName,
            phone: phone,
            email: request.Email,
            birthday: request.Birthday,
            documentNumber: request.DocumentNumber,
            address: request.Address,
            notes: request.Notes,
            acceptsMarketing: request.AcceptsMarketing);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {FullName} ({CustomerId}) creado en tenant {TenantId}",
            customer.FullName, customer.Id, customer.TenantId);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, MapToResponse(customer));
    }

    /// <summary>Edita un cliente existente. Permite reactivarlo (IsActive=true).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(BuildModelState(validation));

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();

        var newPhone = NormalizePhone(request.Phone);
        var phoneChanged = !string.Equals(customer.Phone, newPhone, StringComparison.Ordinal);

        // Si va a quedar activo y el teléfono cambió, validar colisión con otros activos
        if (request.IsActive && phoneChanged)
        {
            var phoneTaken = await _db.Customers
                .AnyAsync(c => c.Id != id && c.IsActive && c.Phone == newPhone, ct);

            if (phoneTaken)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Teléfono duplicado",
                    Detail = $"Ya existe otro cliente activo con el teléfono {newPhone}.",
                    Status = StatusCodes.Status409Conflict
                });
            }
        }

        // Mutación vía métodos verbales que preservan invariantes.
        customer.Rename(request.FullName);
        customer.UpdateContact(newPhone, request.Email, request.Address);
        customer.UpdateProfile(request.DocumentNumber, request.Birthday, request.Notes);

        if (request.AcceptsMarketing) customer.OptInMarketing();
        else customer.OptOutMarketing();

        if (request.IsActive) customer.Reactivate();
        else customer.Archive();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {CustomerId} actualizado en tenant {TenantId}",
            customer.Id, customer.TenantId);

        return Ok(MapToResponse(customer));
    }

    /// <summary>Borrado lógico: marca el cliente como inactivo.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();

        if (!customer.IsActive) return NoContent();

        customer.Archive();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cliente {CustomerId} archivado en tenant {TenantId}",
            customer.Id, customer.TenantId);

        return NoContent();
    }

    /// <summary>
    /// Normaliza el teléfono: trim de espacios al inicio/fin y de espacios múltiples internos.
    /// No removemos formato (espacios, guiones, +) porque es información útil para mostrarlo legible.
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim();
        // Colapsar espacios múltiples a uno solo
        return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
    }

    private static CustomerResponse MapToResponse(Customer c) => new()
    {
        Id = c.Id,
        FullName = c.FullName,
        Phone = c.Phone,
        Email = c.Email,
        Birthday = c.Birthday,
        DocumentNumber = c.DocumentNumber,
        Address = c.Address,
        Notes = c.Notes,
        AcceptsMarketing = c.AcceptsMarketing,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
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
