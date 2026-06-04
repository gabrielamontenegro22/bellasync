using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.IsActive)
            .IsRequired();

        // Política de pagos del salón (configurable por salón).
        // Defaults explícitos para que salones legacy queden con los
        // valores históricos al correr la migración.
        builder.Property(t => t.HoldDurationHours).IsRequired().HasDefaultValue(3);
        builder.Property(t => t.HoldMinBeforeAppointmentMinutes).IsRequired().HasDefaultValue(30);
        builder.Property(t => t.MinAdvanceMinutes).IsRequired().HasDefaultValue(30);

        // Comisiones opt-in. Default false → salones existentes quedan
        // sin el módulo activo (no rompe nada). La admin lo activa
        // explícitamente desde Configuración.
        builder.Property(t => t.CommissionsEnabled).IsRequired().HasDefaultValue(false);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
