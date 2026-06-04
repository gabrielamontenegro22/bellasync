using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Tenants.UploadLogo;

public sealed class UploadLogoHandler : ICommandHandler<UploadLogoCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IFileStorage _storage;
    private readonly ILogger<UploadLogoHandler> _logger;

    public UploadLogoHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IFileStorage storage,
        ILogger<UploadLogoHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(UploadLogoCommand command, CancellationToken ct)
    {
        if (!_currentTenant.HasTenant)
            return ApplicationError.Unauthorized("tenant.no_tenant", "Sesión inválida.");

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);
        if (tenant is null)
            return ApplicationError.NotFound("tenant.not_found", "Salón no encontrado.");

        // Si había un logo viejo nuestro (URL relativa /uploads/...), lo
        // borramos del disco. Si era URL externa (http://...), no la
        // tocamos — alguien la subió a un CDN externo.
        var oldLogo = tenant.LogoUrl;

        try
        {
            tenant.UpdateInfo(
                address: tenant.Address,
                phone: tenant.Phone,
                contactEmail: tenant.ContactEmail,
                logoUrl: command.NewLogoUrl,
                instagramHandle: tenant.InstagramHandle,
                description: tenant.Description);
        }
        catch (DomainException ex)
        {
            return ApplicationError.Validation("tenant.invalid_logo", ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        // Después de persistir el nuevo, borramos el viejo. Si falla el
        // delete, no rollback — el nuevo logo ya está activo, el viejo
        // queda huérfano en disco (loguea WARN dentro del storage).
        if (!string.IsNullOrWhiteSpace(oldLogo) && oldLogo != command.NewLogoUrl)
        {
            await _storage.DeleteAsync(oldLogo, ct);
        }

        _logger.LogInformation("Logo del tenant {TenantId} actualizado", tenant.Id);
        return Result.Success();
    }
}
