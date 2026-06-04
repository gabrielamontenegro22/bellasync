namespace BellaSync.Application.Features.Expenses.Dtos;

/// <summary>
/// DTO de salida de un egreso. Plano — el frontend solo necesita pintar
/// una fila en la tabla "Egresos del día".
/// </summary>
public class ExpenseResponse
{
    public Guid Id { get; set; }

    /// <summary>Texto libre que escribió la admin ("Compra tintes Wella").</summary>
    public string Concept { get; set; } = string.Empty;

    /// <summary>Monto del egreso (positivo).</summary>
    public decimal Amount { get; set; }

    /// <summary>"Cash" / "Transfer" / "Card" / "Other"</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Banco (si Transfer) o marca (si Card). Null para Cash.</summary>
    public string? Provider { get; set; }

    public Guid? RegisteredByUserId { get; set; }
    /// <summary>Nombre del user que registró el egreso (para auditoría en /caja).</summary>
    public string? RegisteredByUserName { get; set; }
    public DateTime RegisteredAt { get; set; }
}
