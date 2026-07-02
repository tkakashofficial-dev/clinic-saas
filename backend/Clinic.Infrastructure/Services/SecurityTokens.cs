using System.Security.Cryptography;
using System.Text;

namespace Clinic.Infrastructure.Services;

/// <summary>Shared primitives for single-use security tokens.</summary>
internal static class SecurityTokens
{
    /// <summary>Cryptographically random, URL-safe token.</summary>
    public static string CreateUrlSafe() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
