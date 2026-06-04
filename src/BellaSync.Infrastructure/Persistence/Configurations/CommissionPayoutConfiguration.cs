using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de CommissionPayout. La query típica es "para este
/// stylist, ¿cuál es el último payout?" → índice por
/// (tenant_id, stylist_id, period_to desc).
/// </summary>
public class CommissionPayoutConfiguration : IEntityTypeConfiguration<CommissionPayout>
{
    public void Configure(EntityTypeBuilder<CommissionPayout> builder)
    {
        builder.ToTable("commission_payouts");

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.TenantId).IsRequired();
        builder.Property(cp => cp.StylistId).IsRequired();

        builder.Property(cp => cp.Amount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(cp => cp.PeriodFrom).IsRequired();
        builder.Property(cp => cp.PeriodTo).IsRequired();
        builder.Property(cp => cp.PaidAt).IsRequired();
        builder.Property(cp => cp.PaidByUserId);
        builder.Property(cp => cp.Notes).HasMaxLength(300);

        builder.Property(cp => cp.CreatedAt).IsRequired();
        builder.Property(cp => cp.UpdatedAt);

        // Índice para "último payout del estilista X" y para listar por
        // tenant ordenado por período.
        builder.HasIndex(cp => new { cp.TenantId, cp.StylistId, cp.PeriodTo });

        // FKs
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(cp => cp.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cp => cp.Stylist)
            .WithMany()
            .HasForeignKey(cp => cp.StylistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(cp => cp.PaidByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
