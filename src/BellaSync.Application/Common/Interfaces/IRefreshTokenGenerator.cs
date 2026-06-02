namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Genera refresh tokens criptográficamente seguros y los hashea con
/// SHA256 para almacenar solo el hash en BD.
///
/// Implementación concreta en Infrastructure.
/// </summary>
public interface IRefreshTokenGenerator
{
    /// <summary>
    /// Genera un nuevo refresh token. Devuelve el plaintext (que solo viaja
    /// al cliente una vez) y el hash (que se persiste en BD).
    /// </summary>
    (string PlaintextToken, string TokenHash) Generate();

    /// <summary>
    /// Calcula el hash de un token plaintext recibido del cliente.
    /// Usado para buscar el token en BD durante el endpoint /refresh.
    /// </summary>
    string Hash(string plaintextToken);
}
