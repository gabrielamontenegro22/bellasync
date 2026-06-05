using BellaSync.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellaSync.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.IsActive)
            .IsRequired();

        // Política de pagos del salón (configurable por salón).
        // Defaults explícitos para que salones legacy queden con los
        // valores históricos al correr la migración.
        builder.Property(t => t.HoldDurationHours).IsRequired().HasDefaultValue(3);
        builder.Property(t => t.HoldMinBeforeAppointmentMinutes).IsRequired().HasDefaultValue(30);
        builder.Property(t => t.MinAdvanceMinutes).IsRequired().HasDefaultValue(30);

        // Comisiones opt-in. Default false → salones existentes quedan
        // sin el módulo activo (no rompe nada). La admin lo activa
        // explícitamente desde Configuración.
        builder.Property(t => t.CommissionsEnabled).IsRequired().HasDefaultValue(false);

        // Info general del salón — todos opcionales.
        builder.Property(t => t.Address).HasMaxLength(200);
        builder.Property(t => t.Phone).HasMaxLength(30);
        builder.Property(t => t.ContactEmail).HasMaxLength(150);
        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.InstagramHandle).HasMaxLength(50);
        builder.Property(t => t.Description).HasMaxLength(500);

        // Flags del horario. LunchBreak default OFF, horas 13-14 si se
        // activa. Holidays default OFF también — la admin decide
        // explícitamente. Los días-franjas y los cierres puntuales
        // viven en salon_weekly_hours y salon_closed_dates.
        builder.Property(t => t.LunchBreakEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.LunchBreakFromHour).IsRequired().HasDefaultValue(13);
        builder.Property(t => t.LunchBreakToHour).IsRequired().HasDefaultValue(14);
        builder.Property(t => t.IsHolidaysClosed).IsRequired().HasDefaultValue(false);

        // Permisos de recepción configurables. Defaults conservadores
        // para no romper salones existentes ni asumir confianza por demás:
        //   - Cap egresos: $100.000 COP (recepción hace gastos chicos)
        //   - Cancelar con plata: SÍ (con nota obligatoria — la admin
        //     decide después qué hacer con el dinero)
        //   - Cerrar caja: NO (decisión financiera de admin)
        builder.Property(t => t.ReceptionExpenseCapCop)
            .HasColumnType("numeric(12,2)")
            .HasDefaultValue(100_000m);
        builder.Property(t => t.ReceptionCanCancelWithMoney).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ReceptionCanCloseCash).IsRequired().HasDefaultValue(false);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
