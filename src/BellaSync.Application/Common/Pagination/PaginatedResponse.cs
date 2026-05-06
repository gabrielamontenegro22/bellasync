namespace BellaSync.Application.Common.Pagination;

/// <summary>
/// Wrapper estándar para respuestas paginadas.
/// Usado por listados con muchos elementos (clientes, citas, productos, etc.)
/// </summary>
public class PaginatedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PaginatedResponse<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems) =>
        new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
}
