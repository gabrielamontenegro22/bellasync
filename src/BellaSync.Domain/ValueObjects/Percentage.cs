using BellaSync.Domain.Common;

namespace BellaSync.Domain.ValueObjects;

/// <summary>
/// Porcentaje entre 0 y 100 inclusive. Inmutable.
/// Se construye via Percentage.Create(value) que valida el rango.
///
/// Usado en Service para CommissionPercentage y DepositPercentage.
/// El valor se guarda como decimal con dos decimales (numeric(5,2) en BD).
///
/// Persistencia: EF usa HasConversion en ServiceConfiguration.
/// </summary>
public readonly record struct Percentage
{
    /// <summary>Valor entre 0 y 100 inclusive.</summary>
    public decimal Value { get; }

    private Percentage(decimal value)
    {
        Value = value;
    }

    /// <summary>
    /// Factory con validación. Lanza DomainException si value está fuera de [0, 100].
    /// </summary>
    public static Percentage Create(decimal value)
    {
        if (value < 0m || value > 100m)
            throw new DomainException($"Porcentaje inválido: {value}. Debe estar entre 0 y 100.");

        return new Percentage(value);
    }

    /// <summary>Percentage cero, útil para defaults.</summary>
    public static Percentage Zero => new(0m);

    /// <summary>Aplica este porcentaje a un monto. Útil para calcular comisiones/anticipos.</summary>
    public Money ApplyTo(Money money) => Money.Create(money.Amount * Value / 100m);

    public override string ToString() => $"{Value:N2}%";
}
