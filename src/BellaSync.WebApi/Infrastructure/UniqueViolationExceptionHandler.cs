using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BellaSync.WebApi.Infrastructure;

/// <summary>
/// Atrapa violaciones de constraints únicos de PostgreSQL (SQLSTATE 23505)
/// que escaparon de los chequeos previos del handler (race condition entre
/// el AnyAsync de validación y el SaveChangesAsync) y las traduce a
/// HTTP 409 Conflict con ProblemDetails — en lugar de devolver 500.
///
/// Cualquier otra excepción se deja pasar al manejo default para que sea
/// visible como bug real.
///
/// Vive en WebApi porque depende de tipos HTTP (IExceptionHandler,
/// HttpContext, IHostEnvironment). Infrastructure se mantiene libre de
/// referencias a Microsoft.AspNetCore.*.
/// </summary>
public class UniqueViolationExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<UniqueViolationExceptionHandler> _logger;

    public UniqueViolationExceptionHandler(
        IHostEnvironment env,
        ILogger<UniqueViolationExceptionHandler> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var pgEx = FindPostgresUniqueViolation(exception);
        if (pgEx is null) return false;

        _logger.LogWarning(
            "Unique violation atrapada: constraint={Constraint} table={Table} detail={Detail}",
            pgEx.ConstraintName, pgEx.TableName, pgEx.Detail);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Recurso duplicado",
            Detail = "Ya existe un recurso con los datos enviados. Verifica los campos únicos.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Instance = httpContext.Request.Path
        };

        // Solo agregamos el constraint name en Development para no leakear schema en prod.
        if (_env.IsDevelopment() && !string.IsNullOrEmpty(pgEx.ConstraintName))
        {
            problem.Extensions["constraint"] = pgEx.ConstraintName;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static PostgresException? FindPostgresUniqueViolation(Exception exception)
    {
        // Recorre la cadena de InnerException buscando un PostgresException 23505.
        var current = exception;
        while (current is not null)
        {
            if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return pg;

            if (current is DbUpdateException dbu && dbu.InnerException is PostgresException pg2
                && pg2.SqlState == PostgresErrorCodes.UniqueViolation)
                return pg2;

            current = current.InnerException;
        }
        return null;
    }
}
