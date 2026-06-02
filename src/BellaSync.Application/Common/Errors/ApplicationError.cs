namespace BellaSync.Application.Common.Errors;

/// <summary>
/// Categorización del tipo de error de un caso de uso.
/// El WebApi traduce esto a status codes HTTP.
/// </summary>
public enum ApplicationErrorType
{
    /// <summary>Datos de entrada inválidos. → 400 Bad Request</summary>
    Validation,

    /// <summary>Recurso no encontrado. → 404 Not Found</summary>
    NotFound,

    /// <summary>Conflicto de estado (duplicado, etc.). → 409 Conflict</summary>
    Conflict,

    /// <summary>Acción no permitida para el usuario actual. → 403 Forbidden</summary>
    Forbidden,

    /// <summary>Sesión inválida. → 401 Unauthorized</summary>
    Unauthorized,
}

/// <summary>
/// Error de un caso de uso. Representa un fallo ESPERADO del dominio o de
/// reglas de negocio — no una excepción técnica.
///
/// Code: identificador estable para que el cliente pueda hacer matching
/// (ej. "service.name_taken"). Pensado para i18n en el futuro.
/// Message: texto en español para mostrar al usuario directamente.
/// </summary>
public sealed record ApplicationError(
    ApplicationErrorType Type,
    string Code,
    string Message)
{
    // Helpers para construir errores comunes sin escribir el Type cada vez.
    public static ApplicationError Validation(string code, string message) =>
        new(ApplicationErrorType.Validation, code, message);

    public static ApplicationError NotFound(string code, string message) =>
        new(ApplicationErrorType.NotFound, code, message);

    public static ApplicationError Conflict(string code, string message) =>
        new(ApplicationErrorType.Conflict, code, message);

    public static ApplicationError Forbidden(string code, string message) =>
        new(ApplicationErrorType.Forbidden, code, message);

    public static ApplicationError Unauthorized(string code, string message) =>
        new(ApplicationErrorType.Unauthorized, code, message);
}
