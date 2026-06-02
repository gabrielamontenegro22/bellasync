using BellaSync.Application.Common.Errors;

namespace BellaSync.Application.Common.Results;

/// <summary>
/// Result de un caso de uso. O contiene un valor de éxito, o contiene un
/// ApplicationError. Nunca ambos.
///
/// Diseñado deliberadamente sin librería externa (OneOf, FluentResults)
/// para mantener la dependencia explícita y el código fácil de leer.
///
/// Uso típico desde un handler:
///   return Result&lt;ServiceResponse&gt;.Success(response);
///   return Result&lt;ServiceResponse&gt;.Failure(ApplicationError.NotFound(...));
///
/// Y desde el controller (vía ControllerExtensions):
///   return result.ToActionResult();
/// </summary>
public sealed class Result<TValue>
{
    public TValue? Value { get; }
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(TValue value)
    {
        Value = value;
        Error = null;
    }

    private Result(ApplicationError error)
    {
        Value = default;
        Error = error;
    }

    public static Result<TValue> Success(TValue value) => new(value);
    public static Result<TValue> Failure(ApplicationError error) => new(error);

    // Conversión implícita: `return ApplicationError.NotFound(...);` desde un
    // handler que retorna Result<X> compila directo, sin Result.Failure(...).
    public static implicit operator Result<TValue>(ApplicationError error) => Failure(error);
}

/// <summary>
/// Result sin valor de éxito (para operaciones que devuelven 204 No Content,
/// como Delete). Equivalente a Result&lt;Unit&gt; pero más limpio.
/// </summary>
public sealed class Result
{
    public ApplicationError? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result() { Error = null; }
    private Result(ApplicationError error) { Error = error; }

    public static Result Success() => new();
    public static Result Failure(ApplicationError error) => new(error);

    public static implicit operator Result(ApplicationError error) => Failure(error);
}
