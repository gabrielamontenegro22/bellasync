using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF config para WhatsAppTemplate.
///
/// Unique constraint (TenantId, Kind) — un solo template por tipo por
/// salón. Si la admin trata de crear duplicados, PG 23505 lo rechaza
/// y el ExceptionHandler lo mapea a 409 Conflict.
/// </summary>
public class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.ToTable("whatsapp_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.Kind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Body)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(t => t.IsEnabled).IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        // Unique: 1 template por tipo por tenant.
        builder.HasIndex(t => new { t.TenantId, t.Kind }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
