using System.Security.Cryptography;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Security;

/// <summary>
/// PBKDF2-based password hashing using SHA256, 10000 iterations, 256-bit key.
/// Format: iterations:saltBase64:hashBase64
/// Implements IPasswordHasher from Domain layer for clean architecture compliance.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => PasswordHasherCore.HashPassword(password);
    public bool VerifyPassword(string password, string hashedPassword) => PasswordHasherCore.VerifyPassword(password, hashedPassword);
}

/// <summary>
/// Static core implementation that can be called without DI (e.g., in DbInitializer).
/// </summary>
internal static class PasswordHasherCore
{
    private const int Iterations = 10000;
    private const int KeySize = 256 / 8; // 32 bytes
    private const int SaltSize = 16;     // 128 bits

    /// <summary>
    /// Hashes a password using PBKDF2 with SHA256.
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, Iterations);

        return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        var parts = hash.Split(':');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[1]);
        var storedHash = Convert.FromBase64String(parts[2]);

        var computedHash = ComputeHash(password, salt, iterations);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }
}