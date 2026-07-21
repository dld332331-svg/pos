using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthResult
{
    /// <summary>Whether the authentication was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable result message (e.g., error description).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The authenticated user. Null if authentication failed.</summary>
    public User? User { get; set; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static AuthResult Succeeded(User user) => new()
    {
        Success = true,
        Message = "Login successful.",
        User = user
    };

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    public static AuthResult Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Service for user authentication, logout, and password management.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user with their username and password.
    /// </summary>
    /// <param name="username">The user's login username.</param>
    /// <param name="password">The user's plain-text password (will be hashed for comparison).</param>
    /// <returns>An <see cref="AuthResult"/> indicating success or failure with details.</returns>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// Logs out the specified user and records the event in the audit log.
    /// </summary>
    /// <param name="userId">ID of the user to log out.</param>
    Task LogoutAsync(Guid userId);

    /// <summary>
    /// Changes a user's password after verifying the old password.
    /// </summary>
    /// <param name="userId">ID of the user changing their password.</param>
    /// <param name="oldPassword">The current password for verification.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <returns>True if the password was changed successfully; false if the old password is incorrect.</returns>
    Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword);
}