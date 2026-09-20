using System.Security.Cryptography;

namespace GSAnalytics.Application.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing (no extra NuGet dependency — everything here is in the BCL).
/// Stored format: "{iterations}.{saltBase64}.{hashBase64}", so the iteration count can be raised
/// later without invalidating already-stored hashes.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000; // OWASP 2023+ minimum recommendation for PBKDF2-HMAC-SHA256
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedKey = Convert.FromBase64String(parts[2]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
