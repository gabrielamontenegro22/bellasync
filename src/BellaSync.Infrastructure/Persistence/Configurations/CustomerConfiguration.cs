using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.Email).HasMaxLength(150);

        builder.Property(c => c.Birthday)
            .HasColumnType("date");

        builder.Property(c => c.DocumentNumber).HasMaxLength(30);
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.Property(c => c.AcceptsMarketing).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        // Teléfono único entre clientes ACTIVOS del mismo tenant.
        // Permite reutilizar un teléfono si el cliente fue archivado.
        builder.HasIndex(c => new { c.TenantId, c.Phone })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        // Índices para búsqueda por nombre y filtrado rápido por tenant
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => new { c.TenantId, c.FullName });
    }
}
