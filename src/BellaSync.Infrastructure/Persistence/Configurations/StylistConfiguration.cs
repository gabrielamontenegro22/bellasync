using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class StylistConfiguration : IEntityTypeConfiguration<Stylist>
{
    public void Configure(EntityTypeBuilder<Stylist> builder)
    {
        builder.ToTable("stylists");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .HasMaxLength(30);

        builder.Property(s => s.Color)
            .HasMaxLength(7);

        builder.Property(s => s.HireDate)
            .HasColumnType("date");

        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.UserId);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        // Nombre único entre estilistas ACTIVOS del mismo tenant.
        builder.HasIndex(s => new { s.TenantId, s.FullName })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        builder.HasIndex(s => s.TenantId);

        // Si el estilista tiene un User asociado, debe ser único en todo el sistema
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");
    }
}
