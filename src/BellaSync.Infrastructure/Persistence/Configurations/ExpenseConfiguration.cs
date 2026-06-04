using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de Expense — egresos (gastos) del salón. Estructura
/// idéntica a Payment salvo que: sin AppointmentId, con Concept, sin Tip.
/// </summary>
public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();

        builder.Property(e => e.Concept)
            .IsRequired()
            .HasMaxLength(200);

        // Method como int (enum) — coherente con payments.
        builder.Property(e => e.Method)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Amount)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, amount => Money.Create(amount));

        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.RegisteredByUserId);  // nullable

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        // Índice para el query típico: "todos los egresos del día X del salón Y".
        builder.HasIndex(e => new { e.TenantId, e.RegisteredAt });

        // FKs
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // RegisteredByUserId queda como FK opcional sin navigation property.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.RegisteredByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
