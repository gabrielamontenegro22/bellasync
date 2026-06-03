using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de Payment — pago FINAL recibido en sitio cuando la
/// cita se completa. NO confundir con PaymentVoucher (anticipo online
/// que se valida visualmente).
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.AppointmentId).IsRequired();

        // Method como int (enum) — más compacto que string y nos protege
        // ante typos en queries.
        builder.Property(p => p.Method)
            .IsRequired()
            .HasConversion<int>();

        // Money se persiste como numeric(12,2). Mismo patrón que Service y voucher.
        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(p => p.Tip)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(p => p.Reference).HasMaxLength(150);
        builder.Property(p => p.RegisteredAt).IsRequired();
        builder.Property(p => p.RegisteredByUserId);  // nullable

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        // Índices:
        //  - (tenant_id, appointment_id) — uso típico: "todos los pagos de
        //    esta cita" desde el panel detalle del agenda.
        //  - (tenant_id, registered_at) — reportes de caja por día/mes.
        builder.HasIndex(p => new { p.TenantId, p.AppointmentId });
        builder.HasIndex(p => new { p.TenantId, p.RegisteredAt });

        // FKs
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Appointment)
            .WithMany()
            .HasForeignKey(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // RegisteredByUserId queda como FK opcional sin navigation property —
        // no necesitamos cargar el User típicamente.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.RegisteredByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
