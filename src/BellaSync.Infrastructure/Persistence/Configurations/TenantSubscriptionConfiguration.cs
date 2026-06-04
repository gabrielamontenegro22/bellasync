using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF config para TenantSubscription. 1:1 con Tenant — unique en TenantId
/// para enforcing en DB que no haya 2 subscriptions para el mismo salón.
/// </summary>
public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.PlanCode)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.CurrentPeriodEnd).IsRequired();
        builder.Property(s => s.TrialEndsAt);
        builder.Property(s => s.CancelledAt);
        builder.Property(s => s.CancellationReason).HasMaxLength(500);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        // 1:1 — un solo subscription por tenant.
        builder.HasIndex(s => s.TenantId).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
