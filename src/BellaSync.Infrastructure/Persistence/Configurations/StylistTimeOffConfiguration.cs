using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration de StylistTimeOff (vacaciones / días libres por
/// estilista). Índice por (StylistId, FromDate) para que la query
/// típica "¿está libre Camila el 17 jul?" sea rápida.
///
/// NO hay unique constraint: permitimos solapamientos del mismo
/// estilista (la admin puede agregar "vacaciones largas" y después
/// "cita médica" dentro). La query de disponibilidad hace OR.
/// </summary>
public class StylistTimeOffConfiguration : IEntityTypeConfiguration<StylistTimeOff>
{
    public void Configure(EntityTypeBuilder<StylistTimeOff> builder)
    {
        builder.ToTable("stylist_time_offs");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.StylistId).IsRequired();
        builder.Property(t => t.FromDate).IsRequired();
        builder.Property(t => t.ToDate).IsRequired();
        builder.Property(t => t.Reason).HasMaxLength(200);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        // Para la query del validator: dado un slot start/end (que mapea
        // a un día Colombia), buscar TimeOff del stylist que incluya
        // esa fecha. Filtra rápido por StylistId.
        builder.HasIndex(t => new { t.StylistId, t.FromDate });

        // FK al estilista. Cascade: si se borra el estilista (hard delete),
        // se borran sus períodos de TimeOff. Pero recuerden que los
        // estilistas se archivan (soft delete), no borran — esto rara vez
        // se dispara.
        builder.HasOne(t => t.Stylist)
            .WithMany()
            .HasForeignKey(t => t.StylistId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK al tenant (consistencia multi-tenant).
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
