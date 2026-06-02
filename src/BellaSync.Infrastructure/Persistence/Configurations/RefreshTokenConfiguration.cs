using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();

        // SHA256 hex = 64 chars. Damos 100 por margen.
        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(100);

        // Búsqueda por TokenHash en cada /refresh → índice único
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Para listar/revocar todos los tokens de un user (logout, password change)
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.RevokedAt);

        builder.Property(t => t.ReplacesTokenHash).HasMaxLength(100);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(100);

        builder.Property(t => t.CreatedByIp).HasMaxLength(50);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        // FK a users con cascade: si se borra el user, sus refresh tokens también
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
