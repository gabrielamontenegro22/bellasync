using BellaSync.Domain.Common;

namespace BellaSync.Domain.ValueObjects;

/// <summary>
/// Cantidad de dinero en pesos colombianos (COP).
/// Inmutable. Se construye via Money.Create(amount) que valida amount &gt;= 0.
///
/// Decisión deliberada: hoy solo soportamos COP (el SaaS opera en Colombia).
/// Si en el futuro se internacionaliza, se agrega un campo Currency y se hace
/// una migración. No vale la pena el costo del campo hoy.
///
/// Persistencia: EF lo guarda como numeric(12,2) usando HasConversion
/// (ver ServiceConfiguration). Una sola columna, sin overhead.
/// </summary>
public readonly record struct Money
{
    /// <summary>Monto en COP, siempre &gt;= 0.</summary>
    public decimal Amount { get; }

    private Money(decimal amount)
    {
        Amount = amount;
    }

    /// <summary>
    /// Factory con validación. Lanza DomainException si amount es negativo.
    /// </summary>
    public static Money Create(decimal amount)
    {
        if (amount < 0)
            throw new DomainException($"Monto inválido: {amount}. Debe ser >= 0.");

        return new Money(amount);
    }

    /// <summary>Money cero, útil para defaults y comparaciones.</summary>
    public static Money Zero => new(0m);

    public override string ToString() => $"{Amount:N2} COP";
}
