using BellaSync.Application.Common.Interfaces;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación productiva de IClock: devuelve el tiempo real del sistema.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
