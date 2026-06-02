namespace BellaSync.Domain.Common;

/// <summary>
/// Excepción lanzada cuando una invariante del dominio se viola.
/// Es defensa en profundidad: los validators de FluentValidation atrapan
/// estos casos antes en el flujo de Application, pero el dominio se protege
/// igual para que no se pueda construir un estado inválido por ninguna vía
/// (jobs, tests, código nuevo que olvide el validator).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
