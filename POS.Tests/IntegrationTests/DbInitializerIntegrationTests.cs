#nullable enable

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Database;
using POS.Infrastructure.Security;
using System.Text.Json;

namespace POS.Tests.IntegrationTests;

/// <summary>
/// Integration tests for DbInitializer using a real SQL Server LocalDB instance.
/// Covers OnModelCreating (entity configurations, query filters), MigrateAsync schema creation,
/// and all seed data paths (admin user, settings).
/// 
/// A unique test database is created per test class run and dropped on disposal.
/// </summary>
public sealed class DbInitializerIntegrationTests : IAsyncLifetime
{
    private const string LocalDbServer = @"(localdb)\MSSQLLocalDB";
    private readonly string _databaseName;
    private readonly string _connectionString;
    private readonly DbContextOptions<POSDbContext> _options;

    public DbInitializerIntegrationTests()
    {
        _databaseName = $"POS_Test_DbInit_{Guid.NewGuid():N}";
        _connectionString = $"Server={LocalDbServer};Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";
        _options = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
    }

    private static readonly PasswordHasher PasswordHasherInstance = new();

    public async Task InitializeAsync()
    {
        // Create the database schema using the EF Core migration pipeline with MigrateAsync.
        // This creates the __EFMigrationsHistory table alongside all entity tables.
        // MigrateAsync is idempotent: subsequent calls (via SeedData's internal MigrateAsync)
        // will detect the migration is already applied and skip it.
        // IMPORTANT: We must NOT use EnsureCreatedAsync here â€” it creates tables without the
        // __EFMigrationsHistory table, causing conflicts when MigrateAsync runs afterward.
        await using var context = new POSDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Drop the test database to clean up
        await using var context = new POSDbContext(_options);
        try
        {
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    /// <summary>
    /// Creates a fresh context pointing to the same database.
    /// </summary>
    private POSDbContext CreateContext()
    {
        return new POSDbContext(_options);
    }

    // ========================================================================
    // Database Schema â€” OnModelCreating Verification
    // ========================================================================

    [Fact]
    public async Task DatabaseSchema_CreatedSuccessfully_AllTablesExist()
    {
        await using var context = CreateContext();

        // Act â€” query INFORMATION_SCHEMA for all expected tables
        var tables = await context.Database
            .SqlQuery<string>($@"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME")
            .ToListAsync();

        // Assert â€” all core entity tables exist
        tables.Should().Contain("Users");
        tables.Should().Contain("Categories");
        tables.Should().Contain("Products");
        tables.Should().Contain("InventoryItems");
        tables.Should().Contain("Recipes");
        tables.Should().Contain("RecipeIngredients");
        tables.Should().Contain("ModifierGroups");
        tables.Should().Contain("Modifiers");
        tables.Should().Contain("ModifierSizes");
        tables.Should().Contain("Tables");
        tables.Should().Contain("Rooms");
        tables.Should().Contain("KitchenStations");
        tables.Should().Contain("Sales");
        tables.Should().Contain("SaleItems");
        tables.Should().Contain("SaleItemModifiers");
        tables.Should().Contain("Payments");
        tables.Should().Contain("Customers");
        tables.Should().Contain("Suppliers");
        tables.Should().Contain("PurchaseOrders");
        tables.Should().Contain("PurchaseOrderItems");
        tables.Should().Contain("InventoryMovements");
        tables.Should().Contain("Shifts");
        tables.Should().Contain("Expenses");
        tables.Should().Contain("WithdrawalDeposits");
        tables.Should().Contain("Printers");
        tables.Should().Contain("Registers");
        tables.Should().Contain("AuditLogs");
        tables.Should().Contain("Settings");
        tables.Should().Contain("BackupRecords");
        tables.Should().Contain("HeldSales");
        tables.Should().Contain("Returns");
        tables.Should().Contain("ReturnItems");
    }

    // ========================================================================
    // Seed Data â€” Admin User
    // ========================================================================

    [Fact]
    public async Task SeedData_CreatesAdminUser_WithCorrectCredentials()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert â€” admin user exists
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        admin.Should().NotBeNull();
        admin!.FullName.Should().Be("System Administrator");
        admin.ArabicName.Should().Be("مدير النظام");
        admin.Role.Should().Be(UserRole.Admin);
        admin.IsActive.Should().BeTrue();
        admin.IsDeleted.Should().BeFalse();

        // Password hash is verifiable
        PasswordHasherInstance.VerifyPassword("123456", admin.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task SeedData_AdminUser_MustChangePasswordOnFirstLogin()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert — spec §32.4 (Password Policy): the initial seeded password is temporary
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        admin.Should().NotBeNull();
        admin!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task SeedData_AdminUser_UsesProvidedInitialPassword()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context, "Str0ng!Pass");

        // Assert
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        admin.Should().NotBeNull();
        PasswordHasherInstance.VerifyPassword("Str0ng!Pass", admin!.PasswordHash).Should().BeTrue();
        PasswordHasherInstance.VerifyPassword("123456", admin.PasswordHash).Should().BeFalse();
    }

    [Fact]
    public async Task SeedData_RolePermissions_IncludeManagePromotions()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert — ManagePromotions is a granular permission (spec §28) granted to Admin and Manager
        var permissions = await context.Settings.FirstOrDefaultAsync(s => s.Key == "RolePermissions");
        permissions.Should().NotBeNull();
        permissions!.Value.Should().Contain("ManagePromotions");
    }

    [Fact]
    public async Task SeedData_DoesNotDuplicateAdminUser_WhenAlreadyExists()
    {
        await using var context = CreateContext();

        // Act â€” seed twice
        await DbInitializer.SeedData(context);

        // Create a fresh context to get a clean query
        await using var context2 = CreateContext();
        await DbInitializer.SeedData(context2);

        // Assert â€” only one admin user
        var adminCount = await context2.Users.CountAsync(u => u.Username == "admin");
        adminCount.Should().Be(1);
    }

    // ========================================================================
    // Seed Data â€” Settings
    // ========================================================================

    [Fact]
    public async Task SeedData_CreatesRolePermissionsSetting()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert
        var permissions = await context.Settings.FirstOrDefaultAsync(s => s.Key == "RolePermissions");
        permissions.Should().NotBeNull();
        permissions!.Category.Should().Be("Security");

        // Verify JSON content contains expected roles
        var json = JsonSerializer.Deserialize<JsonElement>(permissions.Value);
        json.TryGetProperty("Admin", out _).Should().BeTrue();
        json.TryGetProperty("Manager", out _).Should().BeTrue();
        json.TryGetProperty("Cashier", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SeedData_CreatesTaxRateSetting()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert
        var taxRate = await context.Settings.FirstOrDefaultAsync(s => s.Key == "TaxRate");
        taxRate.Should().NotBeNull();
        taxRate!.Value.Should().Be("0.160");
        taxRate.Category.Should().Be("Finance");
    }

    [Fact]
    public async Task SeedData_CreatesCurrencySetting()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert
        var currency = await context.Settings.FirstOrDefaultAsync(s => s.Key == "Currency");
        currency.Should().NotBeNull();
        currency!.Category.Should().Be("Finance");

        var json = JsonSerializer.Deserialize<JsonElement>(currency.Value);
        json.GetProperty("Code").GetString().Should().Be("JOD");
        json.GetProperty("ArabicName").GetString().Should().Be("دينار أردني");
        json.GetProperty("Symbol").GetString().Should().Be("JOD");
        json.GetProperty("DecimalPlaces").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SeedData_CreatesStoreInfoSetting()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert
        var storeInfo = await context.Settings.FirstOrDefaultAsync(s => s.Key == "StoreInfo");
        storeInfo.Should().NotBeNull();
        storeInfo!.Category.Should().Be("General");

        var json = JsonSerializer.Deserialize<JsonElement>(storeInfo.Value);
        json.GetProperty("Name").GetString().Should().Be("POS Store");
        json.GetProperty("ArabicName").GetString().Should().Be("المتجر");
    }

    [Fact]
    public async Task SeedData_CreatesReceiptSettingsSetting()
    {
        await using var context = CreateContext();

        // Act
        await DbInitializer.SeedData(context);

        // Assert
        var receipt = await context.Settings.FirstOrDefaultAsync(s => s.Key == "ReceiptSettings");
        receipt.Should().NotBeNull();
        receipt!.Category.Should().Be("Printing");

        var json = JsonSerializer.Deserialize<JsonElement>(receipt.Value);
        json.GetProperty("ShowLogo").GetBoolean().Should().BeTrue();
        json.GetProperty("ShowBarcode").GetBoolean().Should().BeTrue();
        json.GetProperty("Copies").GetInt32().Should().Be(1);
        json.GetProperty("FooterMessage").GetString().Should().Be("شكراً لزيارتكم");
    }

    [Fact]
    public async Task SeedData_CreatesAllSettings_AndIsIdempotent()
    {
        await using var context = CreateContext();

        // Act â€” first seed
        await DbInitializer.SeedData(context);

        var firstCount = await context.Settings.CountAsync();
        firstCount.Should().Be(5); // RolePermissions, TaxRate, Currency, StoreInfo, ReceiptSettings

        // Act â€” second seed (idempotent)
        await using var context2 = CreateContext();
        await DbInitializer.SeedData(context2);

        var secondCount = await context2.Settings.CountAsync();
        secondCount.Should().Be(firstCount); // No duplicates
    }

    // ========================================================================
    // Soft Delete Query Filter â€” OnModelCreating Behavior
    // ========================================================================

    [Fact]
    public async Task SoftDeleteFilter_ExcludesDeletedEntities_FromQueries()
    {
        await using var context = CreateContext();
        await DbInitializer.SeedData(context);

        // Arrange â€” create a product and mark it as deleted
        var category = new Category
        {
            Name = "Test Category",
            IsActive = true
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = "Deleted Product",
            ArabicName = "Ù…Ù†ØªØ¬ Ù…Ø­Ø°ÙˆÙ",
            Sku = "DEL-001",
            Price = 10.000m,
            Cost = 5.000m,
            CategoryId = category.Id,
            Status = ProductStatus.Active
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var productId = product.Id;

        // Mark as deleted (MarkAsDeleted sets IsDeleted = true)
        product.MarkAsDeleted();
        await context.SaveChangesAsync();

        // Act â€” query should exclude the soft-deleted product
        var visibleProducts = await context.Products
            .Where(p => p.Id == productId)
            .ToListAsync();

        // Assert â€” the deleted product is filtered out by HasQueryFilter
        visibleProducts.Should().BeEmpty();

        // But it still exists in the database (ignore query filter)
        var allProducts = await context.Products
            .IgnoreQueryFilters()
            .Where(p => p.Id == productId)
            .ToListAsync();
        allProducts.Should().HaveCount(1);
        allProducts[0].IsDeleted.Should().BeTrue();
    }

    // ========================================================================
    // MigrateAsync â€” Full Migration Path
    // ========================================================================

    [Fact]
    public async Task MigrateAsync_AppliesAllMigrationsAndSeedsData_Successfully()
    {
        // Create a separate database for this test to verify full EF Core migration pipeline
        var migrationDbName = $"POS_Test_Migrate_{Guid.NewGuid():N}";
        var migrationConnString = $"Server={LocalDbServer};Database={migrationDbName};Trusted_Connection=True;TrustServerCertificate=True;";
        var migrationOptions = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlServer(migrationConnString)
            .Options;

        try
        {
            // Act â€” use the full EF Core migration pipeline to create the schema
            await using var context = new POSDbContext(migrationOptions);
            await context.Database.MigrateAsync();

            // Assert â€” migration history table exists (use ADO.NET to avoid EF Core SqlQuery quirks)
            await using var checkConnection = new SqlConnection(migrationConnString);
            await checkConnection.OpenAsync();
            await using var checkCmd = new SqlCommand(@"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '__EFMigrationsHistory'", checkConnection);
            var migrationTableName = await checkCmd.ExecuteScalarAsync();
            migrationTableName.Should().NotBeNull();
            migrationTableName.Should().Be("__EFMigrationsHistory");

            // Verify all core tables exist (use ADO.NET for consistency)
            await using var tableCmd = new SqlCommand(@"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory'
                ORDER BY TABLE_NAME", checkConnection);
            await using var reader = await tableCmd.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            tables.Should().Contain("Users");
            tables.Should().Contain("Sales");
            tables.Should().Contain("Settings");
            tables.Should().Contain("Products");
            tables.Should().Contain("Categories");
            tables.Should().Contain("Payments");
            tables.Should().Contain("InventoryBatches");
            tables.Should().HaveCount(36); // All entity tables (+ UnitOfMeasures)

            // Verify SeedData works on the migrated schema (SeedData internally calls MigrateAsync again â€” idempotent)
            await DbInitializer.SeedData(context);
            var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            admin.Should().NotBeNull();
            admin!.FullName.Should().Be("System Administrator");
        }
        finally
        {
            // Clean up test database
            await using var cleanupContext = new POSDbContext(migrationOptions);
            await cleanupContext.Database.EnsureDeletedAsync();
        }
    }

    // ========================================================================
    // Unique Constraints â€” OnModelCreating Verification
    // ========================================================================

    [Fact]
    public async Task UniqueConstraint_Username_IsEnforced()
    {
        await using var context = CreateContext();

        // Arrange â€” seed a user with a known username first (don't rely on SeedData ordering)
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "unique_test_admin",
            PasswordHash = PasswordHasherInstance.HashPassword("pass"),
            FullName = "Existing User",
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        // Act â€” try to create a user with the same username
        var duplicateUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "unique_test_admin",
            PasswordHash = PasswordHasherInstance.HashPassword("test"),
            FullName = "Duplicate",
            Role = UserRole.Cashier,
            IsActive = true
        };
        context.Users.Add(duplicateUser);

        // Assert â€” unique constraint violation
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueConstraint_SettingKey_IsEnforced()
    {
        await using var context = CreateContext();

        // Arrange â€” seed a setting with a known key first (don't rely on SeedData ordering)
        var existingSetting = new Setting
        {
            Id = Guid.NewGuid(),
            Key = "UniqueTestSettingKey",
            Value = "original",
            Category = "Test"
        };
        context.Settings.Add(existingSetting);
        await context.SaveChangesAsync();

        // Act â€” try to create a setting with duplicate key
        var duplicateSetting = new Setting
        {
            Id = Guid.NewGuid(),
            Key = "UniqueTestSettingKey",
            Value = "duplicate",
            Category = "Test"
        };
        context.Settings.Add(duplicateSetting);

        // Assert â€” unique constraint violation
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
