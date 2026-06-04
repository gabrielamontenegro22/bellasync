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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<CommissionPayout> CommissionPayouts => Set<CommissionPayout>();
    public DbSet<CashClosing> CashClosings => Set<CashClosing>();
    public DbSet<SalonWeeklyHours> SalonWeeklyHours => Set<SalonWeeklyHours>();
    public DbSet<SalonClosedDate> SalonClosedDates => Set<SalonClosedDate>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Filtro global multi-tenant: cualquier entidad que implemente
        // ITenantEntity sólo devuelve filas cuyo TenantId == TenantId del request.
        //
        // SEMÁNTICA DEFAULT-CERRADA (cambiado el 2026-06):
        // Si no hay tenant en el JWT (request anónimo o sesión vencida),
        // el filtro evalúa "TenantId == Guid.Empty" → no devuelve nada.
        // Los endpoints anónimos legítimos (login, register, forgot/reset password)
        // deben usar IgnoreQueryFilters() explícitamente para escapar el filtro.
        // Esto previene fugas accidentales en cualquier endpoint anónimo nuevo.
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
        // Default-cerrado: siempre filtra por el TenantId actual.
        // Si HasTenant=false, TenantId devuelve Guid.Empty y la query no devuelve filas.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.TenantId == _currentTenant.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-asignación de TenantId para entidades nuevas (defensa en profundidad).
        // Si un handler olvida setear TenantId al crear una entidad ITenantEntity,
        // se completa automáticamente desde el contexto del request actual.
        // Si HasTenant=false (sin sesión), no se toca — el INSERT fallará con
        // violación NOT NULL/FK y la operación se aborta. Comportamiento seguro.
        if (_currentTenant.HasTenant)
        {
            var tenantId = _currentTenant.TenantId;
            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = tenantId;
                }
            }
        }

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
