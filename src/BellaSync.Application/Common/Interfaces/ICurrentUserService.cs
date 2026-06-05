namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Resuelve el UserId del request actual (claim 'sub' del JWT).
/// Devuelve null si no hay usuario autenticado (request anónimo).
///
/// Análogo a ICurrentTenantService pero para el usuario en vez del
/// tenant. Útil para handlers que necesitan trazabilidad (quién hizo
/// la acción), por ejemplo el SuperAdmin que valida un pago de
/// suscripción o el receptionist que cierra caja.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>UserId del autenticado, o null si anónimo.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// Rol del autenticado tal como sale del JWT ("SalonAdmin",
    /// "Receptionist", "Stylist", "SuperAdmin"). Null si anónimo.
    /// Sirve para guards de autorización condicional dentro de handlers
    /// (ej. "cancelar cita pagada requiere admin").
    /// </summary>
    string? Role { get; }

    /// <summary>Atajo común: ¿el user actual es SalonAdmin?</summary>
    bool IsSalonAdmin => Role == "SalonAdmin";
}
