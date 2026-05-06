using System.Linq.Expressions;
using BellaSync.Application.Common.Interfaces;
using BellaSync.Domain.Common;
using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BellaSync.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de la aplicación.
/// Aplica un FILTRO GLOBAL multi-tenant a toda entidad que implemente
/// ITenantEntity, leyendo el TenantId actual desde ICurrentTenantService.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentTenantService _currentTenant;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Stylist> Stylists => Set<Stylist>();
    public DbSet<StylistService> StylistServices => Set<StylistService>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Filtro global multi-tenant: cualquier entidad que implemente
        // ITenantEntity sólo devuelve filas cuyo TenantId == TenantId del request.
        // Si HasTenant es false (registro/login anónimos), el filtro se desactiva.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, new object[] { modelBuilder });
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !_currentTenant.HasTenant || e.TenantId == _currentTenant.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auditoría automática de UpdatedAt para entidades modificadas.
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
