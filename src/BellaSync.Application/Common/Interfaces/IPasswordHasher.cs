namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Hash y verificación de contraseñas. Implementación con BCrypt en Infrastructure.
/// Domain/Application no conocen el algoritmo concreto.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string passwordHash);
}
