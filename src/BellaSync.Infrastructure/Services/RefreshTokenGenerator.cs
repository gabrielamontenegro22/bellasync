using System.Security.Cryptography;
using System.Text;
using BellaSync.Application.Common.Interfaces;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Generación de refresh tokens criptográficamente seguros.
///
/// Token plaintext: 64 bytes (512 bits) de RandomNumberGenerator, codificado
/// como base64url. Es lo que recibe el cliente.
///
/// Hash: SHA256 del plaintext, en hex lowercase. Es lo que se persiste.
/// SHA256 sin sal es OK aquí porque el plaintext es completamente aleatorio
/// y de alta entropía (a diferencia de passwords humanos).
/// </summary>
public class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 64;

    public (string PlaintextToken, string TokenHash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var plaintext = Base64UrlEncode(bytes);
        var hash = Hash(plaintext);
        return (plaintext, hash);
    }

    public string Hash(string plaintextToken)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
            throw new ArgumentException("Token no puede ser vacío.", nameof(plaintextToken));

        var sha = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(sha).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
