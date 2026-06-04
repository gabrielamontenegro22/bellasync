using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF config para WhatsAppMessage.
///
/// Índices clave:
///   - (TenantId, Status, QueuedAt) — el dispatcher levanta Queued del
///     tenant ordenados por antigüedad.
///   - (TenantId, AppointmentId, Kind) — chequeo de idempotencia antes
///     de encolar uno nuevo: "¿ya existe un Reminder24h para esta cita?"
/// </summary>
public class WhatsAppMessageConfiguration : IEntityTypeConfiguration<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.ToTable("whatsapp_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId).IsRequired();

        builder.Property(m => m.Kind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.CustomerPhone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(m => m.RenderedBody)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(m => m.AppointmentId);  // nullable

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.QueuedAt).IsRequired();
        builder.Property(m => m.SentAt);
        builder.Property(m => m.FailedAt);
        builder.Property(m => m.FailureReason).HasMaxLength(500);
        builder.Property(m => m.ExternalMessageId).HasMaxLength(120);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt);

        // Dispatcher: SELECT … WHERE tenant_id=… AND status=0 ORDER BY queued_at
        builder.HasIndex(m => new { m.TenantId, m.Status, m.QueuedAt });

        // Idempotencia: antes de encolar un Reminder24h para una cita,
        // chequear si ya hay uno (Queued/Sent) para esa misma cita+kind.
        builder.HasIndex(m => new { m.TenantId, m.AppointmentId, m.Kind });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // AppointmentId queda como FK opcional sin navigation.
        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(m => m.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
