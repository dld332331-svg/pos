using POS.Application.DTOs;
using POS.Domain.Interfaces;

namespace POS.Application.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync(Guid userId);
    Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
    Task<List<string>> GetUserPermissionsAsync(Guid userId);
    Task<bool> HasPermissionAsync(Guid userId, string permission);

    /// <summary>
    /// Checks whether the database is reachable (spec §13: AUTH-001 "Database Status").
    /// Returns false instead of throwing when the database is unavailable.
    /// </summary>
    Task<bool> CheckDatabaseConnectionAsync();
}