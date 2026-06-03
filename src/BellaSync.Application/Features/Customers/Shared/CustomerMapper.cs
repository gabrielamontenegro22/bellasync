using BellaSync.Application.Features.Customers.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Customers.Shared;

internal static class CustomerMapper
{
    /// <summary>
    /// Mapper "simple" — sin stats derivados. Útil cuando solo necesitas
    /// devolver los campos básicos (ej: respuesta del POST create).
    /// Tag queda en "Nuevo" por defecto.
    /// </summary>
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
        Visits = 0,
        Tag = "Nuevo",
    };

    /// <summary>
    /// Mapper "con stats". Recibe los agregados ya calculados (por subquery
    /// en el handler) y deriva el Tag.
    /// </summary>
    public static CustomerResponse ToResponseWithStats(
        Customer c,
        int visits,
        DateTime? lastVisitAt,
        DateTime? nextVisitAt,
        string? preferredStylistName,
        DateTime utcNow) => new()
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
        Visits = visits,
        LastVisitAt = lastVisitAt,
        NextVisitAt = nextVisitAt,
        PreferredStylistName = preferredStylistName,
        Tag = DeriveTag(visits, lastVisitAt, utcNow),
    };

    /// <summary>
    /// Reglas de clasificación CRM. El orden importa: "Inactivo" sobrescribe
    /// a VIP/Frecuente si llevan mucho sin volver. "Nuevo" es el default
    /// para quienes aún no acumulan suficientes visitas.
    /// </summary>
    public static string DeriveTag(int visits, DateTime? lastVisitAt, DateTime utcNow)
    {
        // Sin historial todavía → siempre Nuevo
        if (visits == 0) return "Nuevo";

        // Si tiene visitas pero la última fue hace más de 90 días → Inactivo
        if (lastVisitAt is not null && (utcNow - lastVisitAt.Value).TotalDays > 90)
            return "Inactivo";

        if (visits >= 15) return "VIP";
        if (visits >= 5) return "Frecuente";
        return "Nuevo";
    }

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
