using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId).IsRequired();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(150);

        // Email único dentro de cada tenant
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.IsActive).IsRequired();

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.LastLoginAt);
    }
}
