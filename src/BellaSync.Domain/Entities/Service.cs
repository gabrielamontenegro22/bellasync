using BellaSync.Domain.Common;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Domain.Entities;

/// <summary>
/// Categorías estándar de servicios ofrecidos por un salón de belleza.
/// Si un salón necesita una categoría que no existe, va a "Otros"
/// hasta que se agregue al enum (con su migración).
/// </summary>
public enum ServiceCategory
{
    Cabello = 0,
    Unas = 1,
    Estetica = 2,
    Maquillaje = 3,
    Depilacion = 4,
    Otros = 99
}

/// <summary>
/// Servicio del catálogo del salón. Aislado por TenantId.
/// Cada salón define los suyos (nombre, precio, duración, comisión).
/// El borrado es lógico: IsActive=false. Las citas pasadas siguen referenciándolo.
///
/// Setters privados: la entidad solo se muta vía métodos verbales que
/// preservan las invariantes (`Rename`, `UpdatePricing`, `EnableDeposit`, etc.).
/// EF Core puede hidratar private setters por reflection — el mapping no se rompe.
/// </summary>
public class Service : BaseEntity, ITenantEntity
{
    // Constructor sin parámetros REQUERIDO por EF Core para materializar
    // entidades desde la BD. Marcado private para que nadie lo use desde C#.
    private Service() { }

    /// <summary>
    /// Factory: crea un Service nuevo con todas las invariantes validadas.
    /// Usar siempre este método (no `new Service { ... }`) para garantizar
    /// que las reglas se aplican.
    /// </summary>
    public static Service Create(
        Guid tenantId,
        string name,
        ServiceCategory category,
        int durationMinutes,
        Money price,
        Percentage commission,
        string? description = null,
        string? color = null,
        bool requiresDeposit = false,
        Percentage? depositPercentage = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del servicio es obligatorio.");
        if (durationMinutes <= 0)
            throw new DomainException("La duración debe ser positiva.");

        var deposit = depositPercentage ?? Percentage.Zero;
        if (requiresDeposit && deposit.Value <= 0m)
            throw new DomainException("Si el servicio requiere anticipo, el porcentaje debe ser > 0.");
        if (!requiresDeposit && deposit.Value > 0m)
            throw new DomainException("Si el servicio no requiere anticipo, el porcentaje debe ser 0.");

        var service = new Service
        {
            TenantId = tenantId,
        };
        service.Name = name.Trim();
        service.Category = category;
        service.DurationMinutes = durationMinutes;
        service.Price = price;
        service.CommissionPercentage = commission;
        service.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        service.Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        service.RequiresDeposit = requiresDeposit;
        service.DepositPercentage = deposit;
        service.IsActive = true;
        return service;
    }

    // TenantId es plumbing multi-tenant (lo setea el SaveChangesAsync auto-set
    // y la interfaz ITenantEntity lo expone con set público). NO es una
    // invariante del negocio — el dominio no decide a qué tenant pertenece.
    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public ServiceCategory Category { get; private set; } = ServiceCategory.Otros;

    /// <summary>Duración estimada del servicio en minutos.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>Precio al público en COP, encapsulado en Money.</summary>
    public Money Price { get; private set; } = Money.Zero;

    /// <summary>
    /// Porcentaje de comisión que se le paga al estilista por este servicio.
    /// Encapsulado en Percentage (0-100 validado).
    /// </summary>
    public Percentage CommissionPercentage { get; private set; } = Percentage.Zero;

    /// <summary>
    /// Color hex (#RRGGBB) para identificarlo visualmente en la agenda.
    /// Opcional; si es null, la UI usa un color por defecto.
    /// </summary>
    public string? Color { get; private set; }

    /// <summary>
    /// Soft delete flag. Cuando es false, el servicio no aparece en listas
    /// de "disponibles" pero sigue referenciado por citas históricas.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Si es true, el servicio requiere que el cliente haga un pago parcial
    /// (anticipo) para confirmar la cita. El monto se calcula con DepositPercentage.
    /// </summary>
    public bool RequiresDeposit { get; private set; }

    /// <summary>
    /// Porcentaje del precio que se cobra como anticipo.
    /// Si RequiresDeposit es false, debe ser Zero (invariante enforzado).
    /// </summary>
    public Percentage DepositPercentage { get; private set; } = Percentage.Zero;

    // ===== MÉTODOS VERBALES =====

    /// <summary>Cambia el nombre. Valida que no sea vacío.</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("El nombre del servicio es obligatorio.");

        Name = newName.Trim();
    }

    /// <summary>Actualiza la descripción (puede ser null para borrarla).</summary>
    public void UpdateDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Cambia categoría.</summary>
    public void Recategorize(ServiceCategory category)
    {
        Category = category;
    }

    /// <summary>Cambia duración. Valida que sea positiva.</summary>
    public void UpdateDuration(int minutes)
    {
        if (minutes <= 0)
            throw new DomainException("La duración debe ser positiva.");
        DurationMinutes = minutes;
    }

    /// <summary>Cambia precio y comisión en una sola operación.</summary>
    public void UpdatePricing(Money price, Percentage commission)
    {
        Price = price;
        CommissionPercentage = commission;
    }

    /// <summary>Cambia el color de la agenda. Null para resetear.</summary>
    public void UpdateColor(string? color)
    {
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
    }

    /// <summary>
    /// Activa el cobro de anticipo con un porcentaje específico.
    /// Lanza si el porcentaje es 0 (sería incoherente con RequiresDeposit=true).
    /// </summary>
    public void EnableDeposit(Percentage percentage)
    {
        if (percentage.Value <= 0m)
            throw new DomainException("El porcentaje de anticipo debe ser > 0.");
        RequiresDeposit = true;
        DepositPercentage = percentage;
    }

    /// <summary>Desactiva el cobro de anticipo. Resetea el porcentaje a 0.</summary>
    public void DisableDeposit()
    {
        RequiresDeposit = false;
        DepositPercentage = Percentage.Zero;
    }

    /// <summary>Soft delete. Idempotente.</summary>
    public void Archive() => IsActive = false;

    /// <summary>Reactivar un servicio archivado. Idempotente.</summary>
    public void Reactivate() => IsActive = true;
}
