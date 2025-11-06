#nullable enable

using System.Security.Cryptography;
using System.Text;
using Api.Managers.InterfacesHelpers;

namespace API.Helpers;

/// <summary>
/// Fournit des méthodes utilitaires cryptographiques.
/// </summary>
public class CryptoHelper : ICryptoHelper
{
    /// <summary>
    /// Converts raw bytes to a Base64 URL-safe string.
    /// </summary>
    /// <param name="data">Input byte array.</param>
    private static string ToBase64Url(byte[] data)
        => Convert.ToBase64String(data)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    /// <summary>
    /// Generates a buffer of cryptographically strong random bytes.
    /// </summary>
    /// <param name="length">Desired byte length.</param>
    private static byte[] RandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentException("length must be positive.", nameof(length));

        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    /// <inheritdoc />
    public string GenerateState(int byteLength)
    {
        if (byteLength <= 0)
            throw new ArgumentException("byteLength must be positive.", nameof(byteLength));

        return ToBase64Url(RandomBytes(byteLength));
    }

    /// <inheritdoc />
    public void GeneratePkce(out string codeVerifier, out string codeChallenge)
    {
        byte[] verifierBytes = RandomBytes(32);
        codeVerifier = ToBase64Url(verifierBytes);

        using SHA256 sha = SHA256.Create();
        byte[] sha256 = sha.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));

        codeChallenge = ToBase64Url(sha256);
    }
}
