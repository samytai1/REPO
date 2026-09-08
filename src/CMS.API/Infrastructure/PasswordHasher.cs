using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Infrastructure;

/// <summary>
/// dbo.AppUser.PasswordHash holds the lowercase hex SHA-256 of the plain password.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Lowercase hex SHA-256 of <paramref name="password"/> (UTF-8 bytes).</summary>
    public static string Hash(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty))).ToLowerInvariant();

    /// <summary>
    /// True when <paramref name="password"/> hashes to <paramref name="storedHash"/>. Compares the
    /// decoded bytes in fixed time, so hex casing does not matter and the check does not leak
    /// how many leading characters matched. A stored value that is not valid hex never matches.
    /// </summary>
    public static bool Matches(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        Span<byte> stored = stackalloc byte[SHA256.HashSizeInBytes];
        var status = Convert.FromHexString(storedHash.Trim(), stored, out _, out var written);
        if (status != OperationStatus.Done || written != stored.Length)
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty), computed);

        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
