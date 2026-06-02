using BellaSync.Application.Common.Results;

namespace BellaSync.Application.Common.Handlers;

/// <summary>
/// Marker para Commands (mutan estado).
/// Un command devuelve Result&lt;TResponse&gt; cuando produce un valor (ej. id
/// del recurso creado) o Result a secas cuando no devuelve nada (Delete).
/// </summary>
public interface ICommand<TResponse> { }
public interface ICommand { }

/// <summary>Marker para Queries (no mutan estado, solo leen).</summary>
public interface IQuery<TResponse> { }

/// <summary>Handler de un Command que produce TResponse.</summary>
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct);
}

/// <summary>Handler de un Command que no produce valor (Delete típicamente).</summary>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken ct);
}

/// <summary>Handler de una Query.</summary>
public interface IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct);
}
