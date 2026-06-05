using System.Security.Claims;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Inventory.ArchiveCategory;
using BellaSync.Application.Features.Inventory.ArchiveProduct;
using BellaSync.Application.Features.Inventory.CreateCategory;
using BellaSync.Application.Features.Inventory.CreateProduct;
using BellaSync.Application.Features.Inventory.Dtos;
using BellaSync.Application.Features.Inventory.GetInventorySummary;
using BellaSync.Application.Features.Inventory.ListCategories;
using BellaSync.Application.Features.Inventory.ListMovements;
using BellaSync.Application.Features.Inventory.ListProducts;
using BellaSync.Application.Features.Inventory.RegisterMovement;
using BellaSync.Application.Features.Inventory.UpdateCategory;
using BellaSync.Application.Features.Inventory.UpdateProduct;
using BellaSync.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Controllers;

/// <summary>
/// Inventario del salón. Productos (catálogo) y movimientos (historial
/// de entradas/salidas/ajustes).
///
/// Autorización:
///   - Lectura (GET): abierta a SalonAdmin + Receptionist.
///     Recepción siempre puede VER aunque no pueda editar — sino el
///     sidebar mostraría un item que la lleva a una pantalla vacía.
///   - Escritura (POST/PUT/DELETE): admin siempre; recepción solo si
///     la admin le activó CanEditInventory en /configuracion/permisos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SalonAdmin,Receptionist")]
public class InventoryController : ControllerBase
{
    // ============================================================
    // QUERIES
    // ============================================================

    /// <summary>
    /// GET /api/Inventory?category=Hair&amp;status=low&amp;query=wella&amp;includeArchived=false
    /// Listado filtrable. Todos los query params son opcionales.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListProductsQuery, IReadOnlyList<ProductResponse>> handler,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] string? query,
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(
            new ListProductsQuery(categoryId, status, query, includeArchived), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Inventory/summary
    /// KPIs del header: total products, valor inventario, low stock count, out count.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InventorySummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(
        [FromServices] IQueryHandler<GetInventorySummaryQuery, InventorySummaryResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetInventorySummaryQuery(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// GET /api/Inventory/{id}/movements
    /// Historial de movimientos del producto (cap 200 desc por fecha).
    /// </summary>
    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductMovementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMovements(
        Guid id,
        [FromServices] IQueryHandler<ListMovementsQuery, IReadOnlyList<ProductMovementResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListMovementsQuery(id), ct);
        return result.ToActionResult();
    }

    // ============================================================
    // COMMANDS — todos requieren CanEditInventory (admin pasa siempre)
    // ============================================================

    /// <summary>POST /api/Inventory — crea producto (stock arranca en 0).</summary>
    [HttpPost]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        [FromServices] ICommandHandler<CreateProductCommand, ProductResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>PUT /api/Inventory/{id} — edita datos del producto. Stock NO se cambia acá.</summary>
    [HttpPut("{id:guid}")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] ICommandHandler<UpdateProductCommand, ProductResponse> handler,
        CancellationToken ct)
    {
        var command = new UpdateProductCommand(
            id, request.Name, request.Brand, request.CategoryId, request.Unit,
            request.MinStock, request.Cost, request.NewStock);
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>POST /api/Inventory/{id}/archive — soft delete. Idempotente.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromServices] ICommandHandler<ArchiveProductCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ArchiveProductCommand(id, false), ct);
        return result.ToActionResult();
    }

    /// <summary>POST /api/Inventory/{id}/reactivate — vuelve a activarlo. Idempotente.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(
        Guid id,
        [FromServices] ICommandHandler<ArchiveProductCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ArchiveProductCommand(id, true), ct);
        return result.ToActionResult();
    }

    // ============================================================
    // CATEGORÍAS — cada salón crea las suyas
    // ============================================================

    /// <summary>GET /api/Inventory/categories?includeArchived=false — lista categorías del tenant.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories(
        [FromServices] IQueryHandler<ListCategoriesQuery, IReadOnlyList<ProductCategoryResponse>> handler,
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(new ListCategoriesQuery(includeArchived), ct);
        return result.ToActionResult();
    }

    /// <summary>POST /api/Inventory/categories — crea categoría custom.</summary>
    [HttpPost("categories")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryCommand command,
        [FromServices] ICommandHandler<CreateCategoryCommand, ProductCategoryResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }

    /// <summary>PUT /api/Inventory/categories/{id} — renombra + cambia color.</summary>
    [HttpPut("categories/{id:guid}")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] ICommandHandler<UpdateCategoryCommand, ProductCategoryResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new UpdateCategoryCommand(id, request.Name, request.Tone), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// POST /api/Inventory/categories/{id}/archive
    /// Idempotente. Devuelve 409 si la categoría tiene productos activos
    /// (forzando a re-categorizar primero).
    /// </summary>
    [HttpPost("categories/{id:guid}/archive")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveCategory(
        Guid id,
        [FromServices] ICommandHandler<ArchiveCategoryCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ArchiveCategoryCommand(id, false), ct);
        return result.ToActionResult();
    }

    /// <summary>POST /api/Inventory/categories/{id}/reactivate — vuelve a activarla.</summary>
    [HttpPost("categories/{id:guid}/reactivate")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateCategory(
        Guid id,
        [FromServices] ICommandHandler<ArchiveCategoryCommand> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ArchiveCategoryCommand(id, true), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// POST /api/Inventory/movements — registra entrada/salida/ajuste.
    /// El usuario que registra se toma del JWT (claim sub).
    /// </summary>
    [HttpPost("movements")]
    [RequireReceptionPermission(Perm.CanEditInventory)]
    [ProducesResponseType(typeof(ProductMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterMovement(
        [FromBody] RegisterMovementRequest request,
        [FromServices] ICommandHandler<RegisterMovementCommand, ProductMovementResponse> handler,
        CancellationToken ct)
    {
        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var parsed)) userId = parsed;

        var command = new RegisterMovementCommand(
            request.ProductId, request.Kind, request.Qty,
            request.Reason, request.Notes, userId);
        var result = await handler.HandleAsync(command, ct);
        return result.ToActionResult();
    }
}

public sealed class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int MinStock { get; set; }
    public decimal Cost { get; set; }

    /// <summary>
    /// Si viene y es distinto al stock actual, el backend crea automáticamente
    /// un movimiento tipo Ajuste para mantener trazabilidad. Si null o igual
    /// al actual, no se toca el stock.
    /// </summary>
    public int? NewStock { get; set; }
}

public sealed class RegisterMovementRequest
{
    public Guid ProductId { get; set; }
    public string Kind { get; set; } = "Inflow";
    public int Qty { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Tone { get; set; } = "Olive";
}
