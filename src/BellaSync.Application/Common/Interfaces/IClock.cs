namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción del "ahora". Inyectada en los handlers que necesitan
/// timestamps (expiraciones, marcas de uso, last login, etc.).
///
/// Propósito principal: testabilidad. En producción usa SystemClock
/// (DateTime.UtcNow). En tests se reemplaza por FakeClock que avanza
/// manualmente — permite probar "el token expiró después de 1h" sin
/// esperar realmente 1h.
///
/// Registrada como Singleton (es stateless y barata).
/// </summary>
public interface IClock
{
    /// <summary>Momento actual en UTC.</summary>
    DateTime UtcNow { get; }
}
