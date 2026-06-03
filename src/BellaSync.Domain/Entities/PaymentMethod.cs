namespace BellaSync.Domain.Entities;

/// <summary>
/// Métodos de pago que el salón acepta. Mantenemos un enum cerrado
/// (en lugar de string libre) porque queremos:
///   - Reportes consistentes ("este mes recibimos $X en Nequi").
///   - Colores y badges predecibles en el UI.
///   - Migraciones controladas cuando agreguemos nuevos métodos.
///
/// "Other" existe como escape: si un cliente paga con un método raro
/// (cheque, divisa, etc.) la recepcionista lo deja en Other y anota
/// el detalle en Reference. Si vemos muchos "Other" del mismo tipo,
/// promovemos ese método al enum con una migración.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Efectivo (billetes / monedas).</summary>
    Cash = 0,

    /// <summary>Transferencia desde la app de Bancolombia.</summary>
    Bancolombia = 1,

    /// <summary>Transferencia desde Nequi.</summary>
    Nequi = 2,

    /// <summary>Transferencia desde Daviplata.</summary>
    Daviplata = 3,

    /// <summary>Tarjeta de crédito en datáfono físico.</summary>
    CreditCard = 4,

    /// <summary>Tarjeta débito en datáfono físico.</summary>
    DebitCard = 5,

    /// <summary>Otro método (cheque, USD efectivo, etc.). Anotar en Reference.</summary>
    Other = 99
}
