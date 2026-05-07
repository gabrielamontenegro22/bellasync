using BellaSync.Domain.Common;

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
/// </summary>
public class Service : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ServiceCategory Category { get; set; } = ServiceCategory.Otros;

    /// <summary>Duración estimada del servicio en minutos.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Precio al público en pesos colombianos (COP), sin impuestos.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Porcentaje de comisión que se le paga al estilista que lo realiza (0-100).
    /// Si es 0, el estilista no recibe comisión por este servicio.
    /// </summary>
    public decimal CommissionPercentage { get; set; }

    /// <summary>
    /// Color hex (#RRGGBB) para identificarlo visualmente en la agenda.
    /// Opcional; si es null, la UI usa un color por defecto.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Soft delete flag. Cuando es false, el servicio no aparece en listas
    /// de "disponibles" pero sigue referenciado por citas históricas.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Si es true, el servicio requiere que el cliente haga un pago parcial
    /// (anticipo) para confirmar la cita. El monto se calcula con DepositPercentage.
    /// </summary>
    public bool RequiresDeposit { get; set; } = false;

    /// <summary>
    /// Porcentaje del precio que se cobra como anticipo (0 a 100).
    /// Solo se aplica cuando RequiresDeposit es true.
    /// Si RequiresDeposit es false, este valor se ignora (típicamente 0).
    /// </summary>
    public decimal DepositPercentage { get; set; } = 0m;
}
