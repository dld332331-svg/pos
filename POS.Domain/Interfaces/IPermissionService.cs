namespace POS.Domain.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string username, string permission);
    Task<string[]> GetPermissionsAsync(string role);
    Task<Dictionary<string, string[]>> GetAllRolePermissionsAsync();
    void InvalidateCache();
}