namespace POS.Domain.Interfaces;

/// <summary>
/// Provides password hashing and verification capabilities.
/// Implemented by infrastructure layer to keep crypto dependencies out of domain.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plain-text password using a secure algorithm (PBKDF2-SHA256).
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies that a plain-text password matches a previously hashed password.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    bool VerifyPassword(string password, string hashedPassword);
}