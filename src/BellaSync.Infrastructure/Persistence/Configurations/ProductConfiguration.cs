using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de Product — catálogo de inventario del salón.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId).IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Brand)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.CategoryId).IsRequired();

        // Unit ahora es legacy nullable — se removió del UI por confusión
        // pero mantenemos la columna para no perder datos viejos.
        builder.Property(p => p.Unit).HasMaxLength(40);

        builder.Property(p => p.Stock).IsRequired();
        builder.Property(p => p.MinStock).IsRequired();

        builder.Property(p => p.Cost)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(p => p.LastInAt);  // nullable
        builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        // Filtros típicos:
        //  - Listado por tenant + categoría
        //  - Buscar por nombre dentro de un tenant
        builder.HasIndex(p => new { p.TenantId, p.CategoryId });
        builder.HasIndex(p => new { p.TenantId, p.IsActive });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK a categoría. Restrict para evitar borrar categorías que tienen
        // productos asignados (el handler de archive valida que no haya
        // productos activos; este FK es defensa en profundidad).
        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
