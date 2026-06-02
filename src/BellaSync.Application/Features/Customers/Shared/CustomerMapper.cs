using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Customers.Shared;

internal static class CustomerMapper
{
    public static CustomerResponse ToResponse(Customer c) => new()
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
        UpdatedAt = c.UpdatedAt,
    };

    /// <summary>
    /// Normaliza el teléfono: trim + colapsar espacios múltiples.
    /// No removemos formato (espacios, guiones, +) porque es información útil
    /// para mostrarlo legible.
    /// </summary>
    public static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim();
        return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
    }
}
