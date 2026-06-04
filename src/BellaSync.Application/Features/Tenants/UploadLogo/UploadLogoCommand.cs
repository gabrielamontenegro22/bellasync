using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Tenants.UploadLogo;

/// <summary>
/// Actualiza el logoUrl del tenant a una URL ya subida (el controller
/// se encarga de guardar el archivo y armar la URL antes de invocar
/// este command). El handler:
///   - Borra el logo viejo del storage si era nuestro (local).
///   - Setea el LogoUrl nuevo en Tenant.
/// </summary>
public sealed record UploadLogoCommand(string NewLogoUrl) : ICommand;
