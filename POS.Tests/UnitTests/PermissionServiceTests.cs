#nullable enable

using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Database;
using POS.Infrastructure.Security;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="PermissionService"/> covering all 4 public methods.
///
/// Uses the EF Core InMemory provider. Each test seeds its own InMemory
/// database with the required <see cref="User"/> and <see cref="Setting"/>
/// rows so that <see cref="PermissionService.GetPermissionsAsync"/>,
/// <see cref="PermissionService.HasPermissionAsync"/> and
/// <see cref="PermissionService.GetAllRolePermissionsAsync"/> execute
/// all branch paths.
///
/// The static <c>_cache</c> and <c>_cacheLastRefresh</c> fields are reset
/// via <see cref="PermissionService.InvalidateCache"/> in the constructor
/// so that tests never interfere with one another.
/// </summary>
public sealed class PermissionServiceTests
{
    private static int _dbCounter;

    public PermissionServiceTests()
    {
        // Reset the static cache so every test starts with clean state.
        // Use a fresh service instance just for the invalidation call.
        using var resetCtx = CreateFreshContext($"__reset_{++_dbCounter}");
        var resetSvc = new PermissionService(resetCtx);
        resetSvc.InvalidateCache();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// Creates a fresh POSDbContext with InMemory provider and a unique
    /// database name.
    /// </summary>
    private static POSDbContext CreateFreshContext(string? tag = null)
    {
        var dbName = tag ?? $"PermTest_{++_dbCounter}_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new POSDbContext(options);
    }

    /// <summary>
    /// Seeds data into a fresh InMemory context and returns both
    /// the context and a PermissionService that uses it.
    /// </summary>
    private (PermissionService Service, POSDbContext Context) BuildService(
        List<User>? users = null,
        List<Setting>? settings = null)
    {
        var context = CreateFreshContext();

        if (users?.Count > 0)
        {
            context.Users.AddRange(users);
            context.SaveChanges();
        }

        if (settings?.Count > 0)
        {
            context.Settings.AddRange(settings);
            context.SaveChanges();
        }

        return (new PermissionService(context), context);
    }

    /// <summary>
    /// Returns the JSON string expected in the RolePermissions setting for
    /// the given role->permissions mapping.
    /// </summary>
    private static string BuildPermissionsJson(params (string role, string[] permissions)[] roles)
    {
        var dict = new Dictionary<string, string[]>();
        foreach (var (role, perms) in roles)
            dict[role] = perms;
        return JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Creates a non-deleted User with the given username and role.
    /// </summary>
    private static User CreateUser(string username = "cashier1", UserRole role = UserRole.Cashier)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = "hash",
            FullName = "Test User",
            Role = role,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a soft-deleted User (IsDeleted = true).
    /// </summary>
    private static User CreateDeletedUser(string username = "deleted_user", UserRole role = UserRole.Cashier)
    {
        var user = CreateUser(username, role);
        user.MarkAsDeleted();
        return user;
    }

    // ========================================================================
    // HasPermissionAsync
    // ========================================================================

    [Fact]
    public async Task HasPermissionAsync_NullUsername_ReturnsFalse()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.HasPermissionAsync(null!, "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_EmptyUsername_ReturnsFalse()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("", "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhitespaceUsername_ReturnsFalse()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("   ", "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_NullPermission_ReturnsFalse()
    {
        var (svc, ctx) = BuildService(users: new List<User> { CreateUser() });
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("cashier1", null!);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_EmptyPermission_ReturnsFalse()
    {
        var (svc, ctx) = BuildService(users: new List<User> { CreateUser() });
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("cashier1", "");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_UserNotFound_ReturnsFalse()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("nonexistent", "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_DeletedUser_ReturnsFalse()
    {
        var (svc, ctx) = BuildService(users: new List<User> { CreateDeletedUser() });
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("deleted_user", "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_UserHasPermission_ReturnsTrue()
    {
        // Arrange
        var json = BuildPermissionsJson(("Cashier", new[] { "Sell", "OpenCashDrawer" }));
        var (svc, ctx) = BuildService(
            users: new List<User> { CreateUser() },
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act
        var result = await svc.HasPermissionAsync("cashier1", "Sell");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_UserDoesNotHavePermission_ReturnsFalse()
    {
        // Arrange
        var json = BuildPermissionsJson(("Cashier", new[] { "Sell", "OpenCashDrawer" }));
        var (svc, ctx) = BuildService(
            users: new List<User> { CreateUser() },
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act — "Backup" is not in the Cashier permissions array
        var result = await svc.HasPermissionAsync("cashier1", "Backup");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_NoRolePermissionsSetting_ReturnsFalse()
    {
        var (svc, ctx) = BuildService(users: new List<User> { CreateUser() });
        using var _ = ctx;
        var result = await svc.HasPermissionAsync("cashier1", "Sell");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_DifferentRoleWithoutPermission_ReturnsFalse()
    {
        // Arrange — Kitchen role does not have "Sell"
        var json = BuildPermissionsJson(
            ("Cashier", new[] { "Sell" }),
            ("Kitchen", new[] { "ViewDashboard" }));
        var (svc, ctx) = BuildService(
            users: new List<User> { CreateUser(username: "chef1", role: UserRole.Kitchen) },
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act
        var result = await svc.HasPermissionAsync("chef1", "Sell");

        // Assert
        result.Should().BeFalse();
    }

    // ========================================================================
    // GetPermissionsAsync
    // ========================================================================

    [Fact]
    public async Task GetPermissionsAsync_NullRole_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync(null!);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_EmptyRole_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_WhitespaceRole_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("   ");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_NoSettingInDb_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_SettingValueEmpty_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = "" }
            });
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_RoleNotInJson_ReturnsEmpty()
    {
        // Arrange — JSON only has "Admin" but we query "Cashier"
        var json = BuildPermissionsJson(("Admin", new[] { "Sell" }));
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_RoleValueNotArray_ReturnsEmpty()
    {
        // Arrange — "Cashier" maps to a string, not an array
        var json = """{"Cashier": "not_an_array"}""";
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_ValidRole_ReturnsPermissions()
    {
        // Arrange
        var json = BuildPermissionsJson(("Cashier", new[] { "Sell", "OpenCashDrawer", "CancelItem" }));
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act
        var result = await svc.GetPermissionsAsync("Cashier");

        // Assert
        result.Should().BeEquivalentTo(new[] { "Sell", "OpenCashDrawer", "CancelItem" });
    }

    [Fact]
    public async Task GetPermissionsAsync_EmptyPermissionArray_ReturnsEmpty()
    {
        // Arrange — role exists but no permissions
        var json = BuildPermissionsJson(("Cashier", Array.Empty<string>()));
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_InvalidJson_ReturnsEmpty()
    {
        // Arrange — malformed JSON
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = "not valid json" }
            });
        using var _ = ctx;
        var result = await svc.GetPermissionsAsync("Cashier");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_CachesResult_ReturnsCachedOnSecondCall()
    {
        // Arrange — create service with valid permissions
        var json = BuildPermissionsJson(("Manager", new[] { "ViewReports", "Sell" }));
        using var context = CreateFreshContext("cache_test");
        context.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Key = "RolePermissions",
            Value = json
        });
        context.SaveChanges();

        var svc = new PermissionService(context);

        // Act — first call loads from DB
        var first = await svc.GetPermissionsAsync("Manager");

        // Remove the setting from the DB — second call should still return
        // cached result (doesn't hit the DB again)
        context.Settings.RemoveRange(context.Settings);
        context.SaveChanges();

        var second = await svc.GetPermissionsAsync("Manager");

        // Assert
        first.Should().BeEquivalentTo(new[] { "ViewReports", "Sell" });
        second.Should().BeEquivalentTo(new[] { "ViewReports", "Sell" });
    }

    [Fact]
    public async Task GetPermissionsAsync_AfterInvalidateCache_ReloadsFromDb()
    {
        // Arrange
        var json1 = BuildPermissionsJson(("Admin", new[] { "Sell" }));
        using var context = CreateFreshContext("inval_cache");
        context.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Key = "RolePermissions",
            Value = json1
        });
        context.SaveChanges();

        var svc = new PermissionService(context);

        // Act — first call caches "Admin" with ["Sell"]
        var first = await svc.GetPermissionsAsync("Admin");
        first.Should().BeEquivalentTo(new[] { "Sell" });

        // Invalidate cache
        svc.InvalidateCache();

        // Now the DB still has the same data, so reload returns same result
        var second = await svc.GetPermissionsAsync("Admin");

        // Assert
        second.Should().BeEquivalentTo(new[] { "Sell" });
    }

    // ========================================================================
    // GetAllRolePermissionsAsync
    // ========================================================================

    [Fact]
    public async Task GetAllRolePermissionsAsync_NoSetting_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService();
        using var _ = ctx;
        var result = await svc.GetAllRolePermissionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRolePermissionsAsync_SettingValueEmpty_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = "" }
            });
        using var _ = ctx;
        var result = await svc.GetAllRolePermissionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRolePermissionsAsync_ValidJson_ReturnsAllRoles()
    {
        // Arrange
        var json = BuildPermissionsJson(
            ("Cashier", new[] { "Sell", "OpenCashDrawer" }),
            ("Admin", new[] { "Sell", "ManageUsers", "Backup" }),
            ("Kitchen", Array.Empty<string>()));
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act
        var result = await svc.GetAllRolePermissionsAsync();

        // Assert
        result.Should().HaveCount(3);
        result["Cashier"].Should().BeEquivalentTo(new[] { "Sell", "OpenCashDrawer" });
        result["Admin"].Should().BeEquivalentTo(new[] { "Sell", "ManageUsers", "Backup" });
        result["Kitchen"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRolePermissionsAsync_InvalidJson_ReturnsEmpty()
    {
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = "not valid" }
            });
        using var _ = ctx;
        var result = await svc.GetAllRolePermissionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRolePermissionsAsync_SkipsNonArrayProperties()
    {
        // Arrange — "string_val" is a string, "number_val" is a number
        var json = """{"Cashier": ["Sell"], "string_val": "not_array", "number_val": 42}""";
        var (svc, ctx) = BuildService(
            settings: new List<Setting>
            {
                new() { Id = Guid.NewGuid(), Key = "RolePermissions", Value = json }
            });
        using var _ = ctx;

        // Act
        var result = await svc.GetAllRolePermissionsAsync();

        // Assert — only the array-valued property is included
        result.Should().HaveCount(1);
        result.Should().ContainKey("Cashier");
    }

    // ========================================================================
    // InvalidateCache
    // ========================================================================

    [Fact]
    public async Task InvalidateCache_ClearsCache_ForcesDbReload()
    {
        // Arrange — seed with initial permissions
        using var context = CreateFreshContext("inval_test");
        context.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Key = "RolePermissions",
            Value = BuildPermissionsJson(("Admin", new[] { "Sell" }))
        });
        context.SaveChanges();

        var svc = new PermissionService(context);

        // Load into cache
        var before = await svc.GetPermissionsAsync("Admin");
        before.Should().BeEquivalentTo(new[] { "Sell" });

        // Update the DB to have different permissions
        context.Settings.RemoveRange(context.Settings);
        context.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(),
            Key = "RolePermissions",
            Value = BuildPermissionsJson(("Admin", new[] { "Backup", "Restore" }))
        });
        context.SaveChanges();

        // Without invalidating the cache, still returns old value
        var stale = await svc.GetPermissionsAsync("Admin");
        stale.Should().BeEquivalentTo(new[] { "Sell" }); // cached

        // Invalidate
        svc.InvalidateCache();

        // Now reloads from DB
        var after = await svc.GetPermissionsAsync("Admin");
        after.Should().BeEquivalentTo(new[] { "Backup", "Restore" });
    }
}
