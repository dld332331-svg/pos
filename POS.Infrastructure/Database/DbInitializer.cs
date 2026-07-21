using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Security;

namespace POS.Infrastructure.Database;

public static class DbInitializer
{
    /// <summary>
    /// Default initial admin password used only when no password is supplied.
    /// The seeded account is always created with MustChangePassword = true (spec §32.4),
    /// so this password must be replaced at first login.
    /// </summary>
    public const string DefaultInitialAdminPassword = "123456";

    public static async Task SeedData(POSDbContext context, string? initialAdminPassword = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Database.MigrateAsync();

        // Seed admin user
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                PasswordHash = PasswordHasherCore.HashPassword(
                    string.IsNullOrWhiteSpace(initialAdminPassword)
                        ? DefaultInitialAdminPassword
                        : initialAdminPassword),
                FullName = "System Administrator",
                ArabicName = "مدير النظام",
                Role = UserRole.Admin,
                IsActive = true,
                IsDeleted = false,
                // Spec §32.4 (Password Policy): the initial seeded password is temporary
                // and must be changed on first login.
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }

        // Seed granular permissions in Settings
        // Uses the fine-grained Permission enum values matching POS.Domain.Enums.Permission
        var permissionsKey = "RolePermissions";
        if (!await context.Settings.AnyAsync(s => s.Key == permissionsKey))
        {
            var defaultPermissions = new Setting
            {
                Id = Guid.NewGuid(),
                Key = permissionsKey,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Admin = new[]
                    {
                        "Sell", "ApplyDiscount", "ChangePrice", "CancelItem",
                        "CancelInvoice", "ReturnItem", "ReturnInvoice", "OpenCashDrawer",
                        "ViewReports", "EditProducts", "EditPrices", "AdjustInventory",
                        "Archive", "Backup", "Restore", "ManageUsers",
                        "ChangeSettings", "ViewDashboard", "ManageTables", "ManageModifiers",
                        "ManageRecipes", "ManagePrinters", "ViewAuditLog", "ManageSuppliers",
                        "ManagePurchases", "ManageCustomers", "ManagePromotions"
                    },
                    Manager = new[]
                    {
                        "Sell", "ApplyDiscount", "ChangePrice", "CancelItem",
                        "CancelInvoice", "ReturnItem", "ReturnInvoice", "OpenCashDrawer",
                        "ViewReports", "EditProducts", "EditPrices", "AdjustInventory",
                        "Archive", "ViewDashboard", "ManageTables", "ManageCustomers",
                        "ManageSuppliers", "ManagePurchases", "ManagePromotions"
                    },
                    Cashier = new[]
                    {
                        "Sell", "CancelItem", "ReturnItem", "OpenCashDrawer", "ViewDashboard"
                    }
                }),
                Description = "Granular role-based permissions using Permission enum values",
                Category = "Security",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Settings.Add(defaultPermissions);
            await context.SaveChangesAsync();
        }

        // Seed default tax rate (16%)
        var taxRateKey = "TaxRate";
        if (!await context.Settings.AnyAsync(s => s.Key == taxRateKey))
        {
            var taxRate = new Setting
            {
                Id = Guid.NewGuid(),
                Key = taxRateKey,
                Value = "0.160",
                Description = "Default tax rate (16%)",
                Category = "Finance",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Settings.Add(taxRate);
            await context.SaveChangesAsync();
        }

        // Seed default currency setting (JOD, 3 decimal places)
        var currencyKey = "Currency";
        if (!await context.Settings.AnyAsync(s => s.Key == currencyKey))
        {
            var currency = new Setting
            {
                Id = Guid.NewGuid(),
                Key = currencyKey,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Code = "JOD",
                    Name = "Jordanian Dinar",
                    ArabicName = "دينار أردني",
                    Symbol = "JOD",
                    DecimalPlaces = 3
                }),
                Description = "Default currency configuration",
                Category = "Finance",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Settings.Add(currency);
            await context.SaveChangesAsync();
        }

        // Seed default store info
        var storeInfoKey = "StoreInfo";
        if (!await context.Settings.AnyAsync(s => s.Key == storeInfoKey))
        {
            var storeInfo = new Setting
            {
                Id = Guid.NewGuid(),
                Key = storeInfoKey,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Name = "POS Store",
                    ArabicName = "المتجر",
                    Address = "",
                    Phone = "",
                    TaxNumber = ""
                }),
                Description = "Store information for receipts and invoices",
                Category = "General",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Settings.Add(storeInfo);
            await context.SaveChangesAsync();
        }

        // Seed default receipt settings
        var receiptKey = "ReceiptSettings";
        if (!await context.Settings.AnyAsync(s => s.Key == receiptKey))
        {
            var receiptSettings = new Setting
            {
                Id = Guid.NewGuid(),
                Key = receiptKey,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ShowLogo = true,
                    ShowBarcode = true,
                    Copies = 1,
                    FooterMessage = "شكراً لزيارتكم",
                    ArabicFooterMessage = "شكراً لزيارتكم"
                }),
                Description = "Receipt printing settings",
                Category = "Printing",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Settings.Add(receiptSettings);
            await context.SaveChangesAsync();
        }

        // Seed default units of measure
        if (!await context.Set<UnitOfMeasure>().AnyAsync())
        {
            var units = new List<UnitOfMeasure>
            {
                // Weight category
                new() { Id = Guid.NewGuid(), Name = "Kilogram", ArabicName = "كيلوغرام", Symbol = "kg", ArabicSymbol = "كغ", Category = "Weight", IsBaseUnit = true, ConversionFactor = 1m, DecimalPlaces = 3, IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Gram", ArabicName = "غرام", Symbol = "g", ArabicSymbol = "غ", Category = "Weight", IsBaseUnit = false, ConversionFactor = 0.001m, DecimalPlaces = 0, IsActive = true, SortOrder = 2, CreatedAt = DateTime.UtcNow },
                // Volume category
                new() { Id = Guid.NewGuid(), Name = "Litre", ArabicName = "لتر", Symbol = "L", ArabicSymbol = "ل", Category = "Volume", IsBaseUnit = true, ConversionFactor = 1m, DecimalPlaces = 3, IsActive = true, SortOrder = 3, CreatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Millilitre", ArabicName = "ميليلتر", Symbol = "mL", ArabicSymbol = "مل", Category = "Volume", IsBaseUnit = false, ConversionFactor = 0.001m, DecimalPlaces = 0, IsActive = true, SortOrder = 4, CreatedAt = DateTime.UtcNow },
                // Count category
                new() { Id = Guid.NewGuid(), Name = "Piece", ArabicName = "قطعة", Symbol = "pc", ArabicSymbol = "قطعة", Category = "Count", IsBaseUnit = true, ConversionFactor = 1m, DecimalPlaces = 0, IsActive = true, SortOrder = 5, CreatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Dozen", ArabicName = "دزينة", Symbol = "dz", ArabicSymbol = "دزينة", Category = "Count", IsBaseUnit = false, ConversionFactor = 12m, DecimalPlaces = 0, IsActive = true, SortOrder = 6, CreatedAt = DateTime.UtcNow },
            };

            context.Set<UnitOfMeasure>().AddRange(units);
            await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();
    }
}