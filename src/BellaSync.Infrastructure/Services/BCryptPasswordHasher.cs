using BellaSync.Application.Common.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación de IPasswordHasher con BCrypt.Net-Next.
/// Work factor 12 — balance estándar entre seguridad y rendimiento (2026).
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(plainPassword));

        return BC.HashPassword(plainPassword, WorkFactor);
    }

    public bool Verify(string plainPassword, string passwordHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            return BC.Verify(plainPassword, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
