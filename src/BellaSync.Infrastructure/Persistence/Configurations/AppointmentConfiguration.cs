using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();

        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.StylistId).IsRequired();
        builder.Property(a => a.ServiceId).IsRequired();

        builder.Property(a => a.StartAt).IsRequired();
        builder.Property(a => a.EndAt).IsRequired();

        // Value Objects: Money + Percentage como HasConversion (mapean a una
        // columna decimal). Patrón consistente con Service.
        builder.Property(a => a.PriceSnapshot)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(a => a.DepositPercentage)
            .IsRequired()
            .HasColumnType("numeric(5,2)")
            .HasConversion(vo => vo.Value, value => Percentage.Create(value));

        builder.Property(a => a.DepositAmount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.DepositStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Channel)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.HoldExpiresAt);

        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.Property(a => a.CancelledAt);
        builder.Property(a => a.CancellationReason).HasMaxLength(500);

        builder.Property(a => a.StartedAt);
        builder.Property(a => a.CompletedAt);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);

        // ===== Índices =====

        // Búsqueda por agenda del día: tenant + stylist + rango de tiempo.
        // El (start_at) primero porque las queries filtran por día.
        builder.HasIndex(a => new { a.TenantId, a.StartAt });
        builder.HasIndex(a => new { a.TenantId, a.StylistId, a.StartAt });

        // Búsqueda por cliente: ver historial / agenda personal.
        builder.HasIndex(a => new { a.TenantId, a.CustomerId, a.StartAt });

        // Background job de release expired holds: índice parcial sobre
        // citas que todavía tienen hold activo.
        builder.HasIndex(a => a.HoldExpiresAt)
            .HasFilter("hold_expires_at IS NOT NULL");

        // ===== FKs =====

        // Tenant: integridad referencial multi-tenant (consistente con el resto).
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer: no se puede borrar un cliente con citas históricas.
        builder.HasOne(a => a.Customer)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Stylist: no se puede borrar un estilista con citas históricas.
        builder.HasOne(a => a.Stylist)
            .WithMany()
            .HasForeignKey(a => a.StylistId)
            .OnDelete(DeleteBehavior.Restrict);

        // Service: no se puede borrar un servicio con citas históricas
        // (el soft delete del servicio mantiene la cita intacta).
        builder.HasOne(a => a.Service)
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
