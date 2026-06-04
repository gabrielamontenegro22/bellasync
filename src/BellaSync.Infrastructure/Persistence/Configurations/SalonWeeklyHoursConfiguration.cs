using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de SalonWeeklyHours. Unique por (tenant_id, day_of_week)
/// — un salón solo puede tener una franja por día (v1; v2 podría
/// permitir múltiples franjas/día y eliminar el unique).
/// </summary>
public class SalonWeeklyHoursConfiguration : IEntityTypeConfiguration<SalonWeeklyHours>
{
    public void Configure(EntityTypeBuilder<SalonWeeklyHours> builder)
    {
        builder.ToTable("salon_weekly_hours");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.TenantId).IsRequired();
        builder.Property(h => h.DayOfWeek).IsRequired();
        builder.Property(h => h.FromHour).IsRequired();
        builder.Property(h => h.ToHour).IsRequired();
        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.UpdatedAt);

        builder.HasIndex(h => new { h.TenantId, h.DayOfWeek }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(h => h.TenantId)
            .OnDelete(DeleteBehavior.Cascade);  // borra horarios si el salón se borra
    }
}
