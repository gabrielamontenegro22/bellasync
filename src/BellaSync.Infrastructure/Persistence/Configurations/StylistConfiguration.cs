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

        builder.Property(s => s.Role)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(s => s.Email)
            .HasMaxLength(150);

        builder.Property(s => s.Phone)
            .HasMaxLength(30);

        builder.Property(s => s.IdNumber)
            .HasMaxLength(30);

        builder.Property(s => s.Color)
            .HasMaxLength(7);

        builder.Property(s => s.HireDate)
            .HasColumnType("date");

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.UserId);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        // Nombre único entre estilistas NO inactivos del mismo tenant.
        // Permite reutilizar nombres si un estilista es archivado (Status=2).
        // Filtro snake_case porque el schema usa UseSnakeCaseNamingConvention.
        builder.HasIndex(s => new { s.TenantId, s.FullName })
            .IsUnique()
            .HasFilter("status <> 2");

        builder.HasIndex(s => s.TenantId);

        // Si el estilista tiene un User asociado, debe ser único en todo el sistema
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");

        // FK física a tenants — garantía de integridad referencial multi-tenant
        // a nivel de BD (defensa en profundidad). Sin navegación inversa para
        // no contaminar Tenant con colecciones de cada entidad hija.
        // OnDelete=Restrict: borrar un tenant con estilistas asociados falla.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
