using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Application.Common.Results;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Application.Features.Stylists.Shared;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BellaSync.Application.Features.Stylists.CreateStylist;

public sealed class CreateStylistHandler : ICommandHandler<CreateStylistCommand, StylistResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IClock _clock;
    private readonly ILogger<CreateStylistHandler> _logger;

    public CreateStylistHandler(
        IApplicationDbContext db,
        ICurrentTenantService currentTenant,
        IClock clock,
        ILogger<CreateStylistHandler> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<StylistResponse>> HandleAsync(
        CreateStylistCommand command, CancellationToken ct)
    {
        var fullName = command.FullName.Trim();

        var nameTaken = await _db.Stylists
            .AnyAsync(s => s.Status != StylistStatus.Inactive && s.FullName == fullName, ct);
        if (nameTaken)
        {
            return ApplicationError.Conflict(
                "stylist.name_taken",
                $"Ya existe un estilista activo con el nombre \"{fullName}\".");
        }

        if (command.ServiceIds.Count > 0)
        {
            var validIds = await _db.Services
                .Where(s => command.ServiceIds.Contains(s.Id) && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var invalidIds = command.ServiceIds.Except(validIds).ToList();
            if (invalidIds.Count > 0)
            {
                return ApplicationError.Validation(
                    "stylist.invalid_services",
                    $"Los siguientes Ids no corresponden a servicios activos del salón: {string.Join(", ", invalidIds)}");
            }
        }

        var stylist = Stylist.Create(
            tenantId: _currentTenant.TenantId,
            fullName: fullName,
            role: command.Role,
            email: command.Email,
            phone: command.Phone,
            idNumber: command.IdNumber,
            color: command.Color,
            hireDate: command.HireDate);

        // Método verbal de la raíz del agregado: protege el invariante
        // "no asignar el mismo servicio dos veces".
        var now = _clock.UtcNow;
        foreach (var serviceId in command.ServiceIds.Distinct())
        {
            stylist.AssignService(serviceId, now);
        }

        _db.Stylists.Add(stylist);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Estilista {FullName} ({StylistId}) creado en tenant {TenantId} con {Count} servicios",
            stylist.FullName, stylist.Id, stylist.TenantId, stylist.StylistServices.Count);

        var created = await _db.Stylists
            .AsNoTracking()
            .Include(s => s.StylistServices)
                .ThenInclude(ss => ss.Service)
            .FirstAsync(s => s.Id == stylist.Id, ct);

        return Result<StylistResponse>.Success(StylistMapper.ToResponse(created));
    }
}
