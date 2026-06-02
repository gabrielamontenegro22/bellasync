using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class StylistServiceConfiguration : IEntityTypeConfiguration<StylistService>
{
    public void Configure(EntityTypeBuilder<StylistService> builder)
    {
        builder.ToTable("stylist_services");

        // Composite primary key
        builder.HasKey(ss => new { ss.StylistId, ss.ServiceId });

        builder.Property(ss => ss.TenantId).IsRequired();
        builder.Property(ss => ss.AssignedAt).IsRequired();

        builder.HasOne(ss => ss.Stylist)
            .WithMany(s => s.StylistServices)
            .HasForeignKey(ss => ss.StylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ss => ss.Service)
            .WithMany() // Service no tiene navegación inversa por simplicidad
            .HasForeignKey(ss => ss.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ss => ss.TenantId);
        builder.HasIndex(ss => ss.ServiceId);

        // FK física a tenants — garantía de integridad referencial multi-tenant
        // también para la tabla intermedia. Las FKs a Stylist y Service ya
        // existen arriba; esta cierra el triángulo y previene asignaciones
        // con TenantId huérfano.
        // OnDelete=Restrict: la limpieza ya ocurre por Cascade de Stylist.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(ss => ss.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
