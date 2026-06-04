using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de CashClosing. Índice ÚNICO en (tenant_id, closed_date)
/// — un cierre por día por salón a nivel BD. Si la admin intenta cerrar
/// dos veces, Postgres rechaza con 23505 y el ExceptionHandler lo mapea
/// a 409 Conflict.
/// </summary>
public class CashClosingConfiguration : IEntityTypeConfiguration<CashClosing>
{
    public void Configure(EntityTypeBuilder<CashClosing> builder)
    {
        builder.ToTable("cash_closings");

        builder.HasKey(cc => cc.Id);

        builder.Property(cc => cc.TenantId).IsRequired();
        builder.Property(cc => cc.ClosedDate).IsRequired();

        // Money fields — todos numeric(12,2) con HasConversion al VO.
        builder.Property(cc => cc.BaseAmount)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));
        builder.Property(cc => cc.CashSales)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));
        builder.Property(cc => cc.CashExpenses)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));
        builder.Property(cc => cc.ExpectedCash)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));
        builder.Property(cc => cc.CountedCash)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));
        builder.Property(cc => cc.TotalAmount)
            .IsRequired().HasColumnType("numeric(12,2)")
            .HasConversion(vo => vo.Amount, x => Money.Create(x));

        // Diff es decimal directo porque puede ser negativo (Money no admite negativos).
        builder.Property(cc => cc.Diff)
            .IsRequired().HasColumnType("numeric(12,2)");

        builder.Property(cc => cc.DiffNote).HasMaxLength(500);
        builder.Property(cc => cc.ClosedAt).IsRequired();
        builder.Property(cc => cc.ClosedByUserId);

        builder.Property(cc => cc.CreatedAt).IsRequired();
        builder.Property(cc => cc.UpdatedAt);

        // ÚNICO por día — invariante "máximo un cierre por día por salón".
        builder.HasIndex(cc => new { cc.TenantId, cc.ClosedDate }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(cc => cc.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nav property ClosedByUser para Include en el historial de cierres
        // — la admin necesita ver "Cerrado por X" en cada fila.
        builder.HasOne(cc => cc.ClosedByUser)
            .WithMany()
            .HasForeignKey(cc => cc.ClosedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
