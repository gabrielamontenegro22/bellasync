using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext usada desde la capa Application.
/// Application no depende de EF Core directamente más allá de DbSet,
/// y se mantiene desacoplada de Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Service> Services { get; }
    DbSet<Stylist> Stylists { get; }
    DbSet<StylistService> StylistServices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
