using BellaSync.Application.Common.Errors;
using BellaSync.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace BellaSync.WebApi.Infrastructure;

/// <summary>
/// Helpers para traducir Result&lt;T&gt; / Result a IActionResult de forma
/// uniforme en todos los controllers.
///
/// Mapeo ApplicationErrorType → HTTP:
///   Validation   → 400 Bad Request
///   NotFound     → 404 Not Found
///   Conflict     → 409 Conflict
///   Forbidden    → 403 Forbidden
///   Unauthorized → 401 Unauthorized
///
/// El payload de error es ProblemDetails con el Code y Message del dominio.
/// </summary>
public static class ResultActionExtensions
{
    public static IActionResult ToActionResult<TValue>(this Result<TValue> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ToProblemDetails(result.Error!);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return ToProblemDetails(result.Error!);
    }

    /// <summary>
    /// Variante para Create: si Result es Success, devuelve 201 con location
    /// hacia GetById. Si es Failure, devuelve ProblemDetails como siempre.
    /// </summary>
    public static IActionResult ToCreatedAtAction<TValue>(
        this Result<TValue> result,
        string actionName,
        Func<TValue, object> routeValues)
    {
        if (result.IsSuccess)
            return new CreatedAtActionResult(actionName, null, routeValues(result.Value!), result.Value);

        return ToProblemDetails(result.Error!);
    }

    private static IActionResult ToProblemDetails(ApplicationError error)
    {
        var status = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ApplicationErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        // RFC 7807: title es legible para humanos. El code técnico va en
        // Extensions["code"] para que el frontend pueda mapear a i18n o
        // mostrar mensajes específicos. Mantiene compat con el frontend
        // actual que usa `detail` primariamente.
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Message,
            Detail = error.Message,
        };
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
