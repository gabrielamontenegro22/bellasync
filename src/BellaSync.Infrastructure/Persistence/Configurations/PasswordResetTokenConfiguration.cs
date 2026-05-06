using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(128);

        // El token es buscado por valor en cada reset → índice único
        builder.HasIndex(t => t.Token).IsUnique();

        // Para limpiar tokens antiguos por usuario rápido
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
