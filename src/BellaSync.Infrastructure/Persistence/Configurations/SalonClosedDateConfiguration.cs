using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de SalonClosedDate. Unique por (tenant_id, closed_date)
/// — un salón no puede tener duplicados.
/// </summary>
public class SalonClosedDateConfiguration : IEntityTypeConfiguration<SalonClosedDate>
{
    public void Configure(EntityTypeBuilder<SalonClosedDate> builder)
    {
        builder.ToTable("salon_closed_dates");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.ClosedDate).IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(200);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        builder.HasIndex(c => new { c.TenantId, c.ClosedDate }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
