namespace BellaSync.Domain.Entities;

/// <summary>
/// Métodos de pago que el salón acepta. Mantenemos un enum cerrado
/// (en lugar de string libre) porque queremos:
///   - Reportes consistentes ("este mes recibimos $X en transferencias").
///   - Colores y badges predecibles en el UI.
///   - Migraciones controladas cuando agreguemos nuevos métodos.
///
/// VERSIÓN 2 (2026-06): colapsamos los bancos individuales en
/// "Transfer" y "Card" para mantener el enum chico. El banco/marca
/// específico vive aparte en la columna `Provider` (string nullable).
///
/// Mapping de valores antiguos al nuevo modelo:
///   Bancolombia(1), Nequi(2), Daviplata(3) → Transfer(1) + Provider
///   CreditCard(4), DebitCard(5)             → Card(2) + Provider
///
/// "Other" existe como escape: si un cliente paga con un método raro
/// (cheque, divisa, criptomonedas) la recepcionista lo deja en Other
/// y anota el detalle en Reference.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Efectivo (billetes / monedas). Provider siempre null.</summary>
    Cash = 0,

    /// <summary>
    /// Transferencia bancaria o billetera digital. Provider debe llevar
    /// el nombre del banco/billetera ("Bancolombia", "Nequi", "Daviplata",
    /// "Davivienda", "BBVA", "Banco de Bogotá", etc.). El nombre se usa
    /// para reconciliar contra el extracto del banco correspondiente.
    /// </summary>
    Transfer = 1,

    /// <summary>
    /// Tarjeta de crédito o débito en datáfono físico. Provider puede
    /// llevar la marca ("Visa", "Mastercard", "American Express",
    /// "Diners Club") — útil pero no obligatorio.
    /// </summary>
    Card = 2,

    /// <summary>Otro método (cheque, USD efectivo, etc.). Anotar en Reference.</summary>
    Other = 99
}
