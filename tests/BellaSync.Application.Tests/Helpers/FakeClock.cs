using BellaSync.Application.Common.Interfaces;

namespace BellaSync.Application.Tests.Helpers;

/// <summary>
/// IClock controlado para tests. El tiempo NO avanza automáticamente —
/// los tests lo mueven con Advance() o set directo.
/// </summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }

    public FakeClock(DateTime? initial = null)
    {
        UtcNow = initial ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Avanza el reloj.</summary>
    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
