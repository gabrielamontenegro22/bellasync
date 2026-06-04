using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.UpdateTenantInfo;

/// <summary>
/// Actualiza info general del salón. Name se acepta acá también — todo
/// se edita junto en la misma pantalla. Slug NO se cambia por acá
/// (impacta URLs públicas; require su propio flow con validación de
/// unicidad y avisos a clientes que tengan la URL guardada).
/// </summary>
public sealed record UpdateTenantInfoCommand(
    string Name,
    string? Address,
    string? Phone,
    string? ContactEmail,
    string? LogoUrl,
    string? InstagramHandle,
    string? Description
) : ICommand<TenantInfoResponse>;
