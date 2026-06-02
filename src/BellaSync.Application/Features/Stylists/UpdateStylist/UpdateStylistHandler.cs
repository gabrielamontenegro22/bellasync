using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Stylists.UpdateStylist;

/// <summary>
/// Edita estilista. Sincroniza la relación M:N con Services: la lista
/// ServiceIds REEMPLAZA completamente las asignaciones actuales.
/// </summary>
public sealed class UpdateStylistHandler : ICommandHandler<UpdateStylistCommand, StylistResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly ILogger<UpdateStylistHandler> _logger;

    public UpdateStylistHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        ILogger<UpdateStylistHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Result<StylistResponse>> HandleAsync(
        UpdateStylistCommand command, CancellationToken ct)
    {
        var stylist = await _db.Stylists
            .Include(s => s.StylistServices)
            .FirstOrDefaultAsync(s => s.Id == command.Id, ct);

        if (stylist is null)
        {
            return ApplicationError.NotFound(
                "stylist.not_found",
                $"No existe un estilista con id {command.Id}.");
        }

        var fullName = command.FullName.Trim();
        var nameChanged = !string.Equals(stylist.FullName, fullName, StringComparison.OrdinalIgnoreCase);

        if (command.Status != StylistStatus.Inactive && nameChanged)
        {
            var nameTaken = await _db.Stylists
                .AnyAsync(s => s.Id != command.Id && s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
            if (nameTaken)
            {
                return ApplicationError.Conflict(
                    "stylist.name_taken",
                    $"Ya existe otro estilista activo con el nombre \"{fullName}\".");
            }
        }

        var requestedServiceIds = command.ServiceIds.Distinct().ToList();
        if (requestedServiceIds.Count > 0)
        {
            var validIds = await _db.Services
                .Where(s => requestedServiceIds.Contains(s.Id) && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var invalidIds = requestedServiceIds.Except(validIds).ToList();
            if (invalidIds.Count > 0)
            {
                return ApplicationError.Validation(
                    "stylist.invalid_services",
                    $"Los siguientes Ids no corresponden a servicios activos del salón: {string.Join(", ", invalidIds)}");
            }
        }

        // Aplicar cambios escalares vía métodos verbales
        stylist.Rename(fullName);
        stylist.ChangeRole(command.Role);
        stylist.UpdateContact(command.Email, command.Phone, command.IdNumber);
        stylist.UpdateColor(command.Color);
        stylist.SetHireDate(command.HireDate);

        switch (command.Status)
        {
            case StylistStatus.Active: stylist.Reactivate(); break;
            case StylistStatus.Vacation: stylist.GoOnVacation(); break;
            case StylistStatus.Inactive: stylist.Archive(); break;
        }

        // Sincronizar relación M:N con servicios
        var currentServiceIds = stylist.StylistServices.Select(ss => ss.ServiceId).ToHashSet();
        var requestedSet = requestedServiceIds.ToHashSet();

        var toRemove = stylist.StylistServices
            .Where(ss => !requestedSet.Contains(ss.ServiceId))
            .ToList();
        foreach (var ss in toRemove) _db.StylistServices.Remove(ss);

        var toAdd = requestedSet.Except(currentServiceIds);
        foreach (var newServiceId in toAdd)
        {
            stylist.StylistServices.Add(new StylistService
            {
                StylistId = stylist.Id,
                ServiceId = newServiceId,
                TenantId = _currentTenant.TenantId,
                AssignedAt = DateTime.UtcNow,
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

        return Result<StylistResponse>.Success(StylistMapper.ToResponse(updated));
    }
}
