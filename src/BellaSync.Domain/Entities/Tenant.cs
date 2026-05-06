using BellaSync.Domain.Common;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Representa un salón de belleza dentro del SaaS.
/// El Tenant es el "dueño" lógico de los datos: usuarios, citas,
/// inventario, clientes, pagos, etc. siempre pertenecen a un Tenant.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Identificador único legible para URLs (ej. "bella-spa-neiva").
    /// Se genera automáticamente a partir del nombre durante el registro.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Relación inversa: usuarios que pertenecen a este salón
    public ICollection<User> Users { get; set; } = new List<User>();
}
