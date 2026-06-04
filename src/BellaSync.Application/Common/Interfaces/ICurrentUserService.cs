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
}
