using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class PaymentVoucherConfiguration : IEntityTypeConfiguration<PaymentVoucher>
{
    public void Configure(EntityTypeBuilder<PaymentVoucher> builder)
    {
        builder.ToTable("payment_vouchers");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId).IsRequired();
        builder.Property(v => v.AppointmentId).IsRequired();

        builder.Property(v => v.ReportedAmount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(v => v.Bank).HasMaxLength(80);
        builder.Property(v => v.ReferenceNumber).HasMaxLength(80);
        builder.Property(v => v.SenderName).HasMaxLength(150);
        builder.Property(v => v.SenderPhone).HasMaxLength(30);
        builder.Property(v => v.ImageUrl).HasMaxLength(500);

        builder.Property(v => v.ReceivedAt).IsRequired();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.DecidedAt);
        builder.Property(v => v.DecidedBy);
        builder.Property(v => v.DecisionNotes).HasMaxLength(500);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt);

        // Índices: cola por urgencia (Pending + tenant)
        builder.HasIndex(v => new { v.TenantId, v.Status });
        builder.HasIndex(v => v.AppointmentId);

        // FKs
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Appointment)
            .WithMany()
            .HasForeignKey(v => v.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
