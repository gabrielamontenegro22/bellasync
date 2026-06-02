using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.Property(s => s.Category)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.DurationMinutes).IsRequired();

        // Money y Percentage son Value Objects. HasConversion los aplana a
        // una columna decimal en BD, manteniendo el schema idéntico al pre-VO.
        // Esto significa que NO hay migración EF necesaria — el cambio es
        // 100% código.
        builder.Property(s => s.Price)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(
                vo => vo.Amount,
                amount => Money.Create(amount));

        builder.Property(s => s.CommissionPercentage)
            .IsRequired()
            .HasColumnType("numeric(5,2)")
            .HasConversion(
                vo => vo.Value,
                value => Percentage.Create(value));

        builder.Property(s => s.RequiresDeposit).IsRequired();
        builder.Property(s => s.DepositPercentage)
            .IsRequired()
            .HasColumnType("numeric(5,2)")
            .HasConversion(
                vo => vo.Value,
                value => Percentage.Create(value));

        builder.Property(s => s.Color)
            .HasMaxLength(7); // #RRGGBB

        builder.Property(s => s.IsActive).IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        // Nombre único entre servicios ACTIVOS del mismo tenant.
        // Permite reutilizar el nombre de un servicio archivado.
        // Filtro snake_case porque el schema usa UseSnakeCaseNamingConvention.
        builder.HasIndex(s => new { s.TenantId, s.Name })
            .IsUnique()
            .HasFilter("is_active = true");

        // Índice general por tenant para filtrado rápido
        builder.HasIndex(s => s.TenantId);

        // FK física a tenants — garantía de integridad referencial multi-tenant
        // a nivel de BD (defensa en profundidad). Sin navegación inversa para
        // no contaminar Tenant con colecciones de cada entidad hija.
        // OnDelete=Restrict: borrar un tenant con servicios asociados falla.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
