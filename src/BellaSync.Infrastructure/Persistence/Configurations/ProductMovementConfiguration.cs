using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de ProductMovement — historial inmutable de movimientos
/// de inventario por producto.
/// </summary>
public class ProductMovementConfiguration : IEntityTypeConfiguration<ProductMovement>
{
    public void Configure(EntityTypeBuilder<ProductMovement> builder)
    {
        builder.ToTable("product_movements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.ProductId).IsRequired();

        builder.Property(m => m.Kind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.Qty).IsRequired();
        builder.Property(m => m.StockBefore).IsRequired();
        builder.Property(m => m.StockAfter).IsRequired();

        builder.Property(m => m.Reason)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Notes).HasMaxLength(500);

        builder.Property(m => m.RegisteredByUserId);  // nullable
        builder.Property(m => m.RegisteredAt).IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt);

        // Query típico: "todos los mov del producto X ordenados por fecha desc".
        builder.HasIndex(m => new { m.TenantId, m.ProductId, m.RegisteredAt });

        // FK fuerte al producto — si el producto se borrara realmente (no
        // soft delete), restringimos. En la práctica los productos se
        // archivan, nunca se borran, así que esto es defensa.
        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.RegisteredByUser)
            .WithMany()
            .HasForeignKey(m => m.RegisteredByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
