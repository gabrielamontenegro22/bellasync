using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF config para SubscriptionInvoice. Índice por (TenantId, IssuedAt
/// desc) para que la query típica "historial del salón" sea rápida.
/// </summary>
public class SubscriptionInvoiceConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
    {
        builder.ToTable("subscription_invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).IsRequired();

        builder.Property(i => i.PlanCode)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(i => i.Amount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(i => i.PeriodStart).IsRequired();
        builder.Property(i => i.PeriodEnd).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();
        builder.Property(i => i.IssuedAt).IsRequired();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.PaidAt);
        builder.Property(i => i.PaymentMethod).HasMaxLength(40);
        builder.Property(i => i.Reference).HasMaxLength(120);
        builder.Property(i => i.Note).HasMaxLength(500);

        // Reporte (paso intermedio anti-pasarela).
        builder.Property(i => i.ReportedAt);
        builder.Property(i => i.ReportedMethod).HasMaxLength(40);
        builder.Property(i => i.ReportedReference).HasMaxLength(120);

        // Validación SuperAdmin.
        builder.Property(i => i.ValidatedByUserId);
        builder.Property(i => i.ValidatedAt);
        builder.Property(i => i.RejectedAt);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);

        builder.HasIndex(i => new { i.TenantId, i.IssuedAt });
        // Acelera la query de la cola de validación.
        builder.HasIndex(i => i.Status);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
