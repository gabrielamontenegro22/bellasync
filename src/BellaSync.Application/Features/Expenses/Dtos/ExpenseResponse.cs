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

    /// <summary>"Cash" / "Bancolombia" / "Nequi" / etc.</summary>
    public string Method { get; set; } = string.Empty;

    public Guid? RegisteredByUserId { get; set; }
    public DateTime RegisteredAt { get; set; }
}
