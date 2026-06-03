namespace BellaSync.Application.Features.Customers.Dtos;

/// <summary>
/// DTO de salida para representar un cliente en respuestas de la API.
///
/// Incluye stats derivados (Visits / LastVisitAt / NextVisitAt /
/// PreferredStylistName / Tag) que se calculan por subquery sobre la
/// tabla de Appointments — no son columnas físicas del cliente.
///
/// Los stats son siempre frescos: cada GET / LIST los recalcula. Es
/// barato porque los queries van todos por índice (customer_id en
/// appointments).
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public bool AcceptsMarketing { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ===== STATS DERIVADOS (proyectados desde Appointments) =====

    /// <summary>Total de citas completadas del cliente.</summary>
    public int Visits { get; set; }

    /// <summary>
    /// Fecha/hora de la última cita completada. null si nunca ha asistido.
    /// </summary>
    public DateTime? LastVisitAt { get; set; }

    /// <summary>
    /// Fecha/hora de la próxima cita futura (Pending o Confirmed).
    /// null si no tiene citas agendadas.
    /// </summary>
    public DateTime? NextVisitAt { get; set; }

    /// <summary>
    /// Nombre del estilista con quien más se ha atendido (más Completadas).
    /// null si no tiene historial.
    /// </summary>
    public string? PreferredStylistName { get; set; }

    /// <summary>
    /// Clasificación derivada para el CRM:
    ///  - "VIP": 15+ visitas
    ///  - "Frecuente": 5-14 visitas
    ///  - "Nuevo": menos de 5 visitas
    ///  - "Inactivo": no ha vuelto en 90+ días (sobrescribe a Frecuente/VIP si aplica)
    /// </summary>
    public string Tag { get; set; } = "Nuevo";
}
