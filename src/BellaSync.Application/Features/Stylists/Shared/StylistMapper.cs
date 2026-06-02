using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Stylists.Shared;

internal static class StylistMapper
{
    public static StylistResponse ToResponse(Stylist s) => new()
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
                Price = ss.Service.Price.Amount,
            })
            .OrderBy(x => x.Name)
            .ToList(),
    };
}
