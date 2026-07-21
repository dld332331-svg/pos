using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;

namespace POS.Infrastructure.Security;

public class PermissionService : IPermissionService
{
    private readonly POSDbContext _context;
    private static readonly ConcurrentDictionary<string, string[]> _cache = new();
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
    private static DateTime _cacheLastRefresh = DateTime.MinValue;

    public PermissionService(POSDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(string username, string permission)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(permission))
            return false;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);

        if (user == null)
            return false;

        var permissions = await GetPermissionsAsync(user.Role.ToString());
        return permissions.Contains(permission);
    }

    public async Task<string[]> GetPermissionsAsync(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Array.Empty<string>();

        // Check cache first
        if (_cache.TryGetValue(role, out var cachedPermissions) &&
            DateTime.UtcNow - _cacheLastRefresh < _cacheExpiration)
        {
            return cachedPermissions;
        }

        // Load from database
        var setting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "RolePermissions" && !s.IsDeleted);

        if (setting == null || string.IsNullOrEmpty(setting.Value))
            return Array.Empty<string>();

        try
        {
            var permissionsDoc = JsonDocument.Parse(setting.Value);
            if (permissionsDoc.RootElement.TryGetProperty(role, out var roleElement) &&
                roleElement.ValueKind == JsonValueKind.Array)
            {
                var permissions = roleElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .OfType<string>()
                    .Where(s => s.Length > 0)
                    .ToArray();

                // Update cache
                _cache[role] = permissions;
                _cacheLastRefresh = DateTime.UtcNow;

                return permissions;
            }
        }
        catch (JsonException)
        {
            // Invalid JSON in settings, return empty
        }

        return Array.Empty<string>();
    }

    public async Task<Dictionary<string, string[]>> GetAllRolePermissionsAsync()
    {
        var setting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "RolePermissions" && !s.IsDeleted);

        if (setting == null || string.IsNullOrEmpty(setting.Value))
            return new Dictionary<string, string[]>();

        try
        {
            var result = new Dictionary<string, string[]>();
            var doc = JsonDocument.Parse(setting.Value);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    result[property.Name] = property.Value.EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .ToArray();
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string[]>();
        }
    }

    /// <summary>
    /// Forces a cache refresh on next permission check.
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Clear();
        _cacheLastRefresh = DateTime.MinValue;
    }
}