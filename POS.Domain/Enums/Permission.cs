namespace POS.Domain.Enums;

/// <summary>
/// Represents all possible permissions in the POS system.
/// Permissions are used to control access to system features and operations.
/// </summary>
[Flags]
public enum Permission
{
    None = 0,

    /// <summary>Allows performing sales transactions.</summary>
    Sell = 1 << 0,

    /// <summary>Allows applying discounts to sale items or invoices.</summary>
    ApplyDiscount = 1 << 1,

    /// <summary>Allows changing the price of a product during a sale.</summary>
    ChangePrice = 1 << 2,

    /// <summary>Allows cancelling an individual item on a sale.</summary>
    CancelItem = 1 << 3,

    /// <summary>Allows cancelling an entire invoice/sale.</summary>
    CancelInvoice = 1 << 4,

    /// <summary>Allows processing item returns.</summary>
    ReturnItem = 1 << 5,

    /// <summary>Allows processing full invoice returns.</summary>
    ReturnInvoice = 1 << 6,

    /// <summary>Allows opening the physical cash drawer.</summary>
    OpenCashDrawer = 1 << 7,

    /// <summary>Allows viewing sales and financial reports.</summary>
    ViewReports = 1 << 8,

    /// <summary>Allows creating and editing products.</summary>
    EditProducts = 1 << 9,

    /// <summary>Allows editing product prices.</summary>
    EditPrices = 1 << 10,

    /// <summary>Allows adjusting inventory quantities.</summary>
    AdjustInventory = 1 << 11,

    /// <summary>Allows archiving records.</summary>
    Archive = 1 << 12,

    /// <summary>Allows performing data backups.</summary>
    Backup = 1 << 13,

    /// <summary>Allows restoring data from backups.</summary>
    Restore = 1 << 14,

    /// <summary>Allows creating and managing user accounts.</summary>
    ManageUsers = 1 << 15,

    /// <summary>Allows changing system settings.</summary>
    ChangeSettings = 1 << 16,

    /// <summary>Allows viewing the main dashboard.</summary>
    ViewDashboard = 1 << 17,

    /// <summary>Allows managing restaurant tables and layout.</summary>
    ManageTables = 1 << 18,

    /// <summary>Allows managing product modifier groups and modifiers.</summary>
    ManageModifiers = 1 << 19,

    /// <summary>Allows managing product recipes.</summary>
    ManageRecipes = 1 << 20,

    /// <summary>Allows managing printer configurations.</summary>
    ManagePrinters = 1 << 21,

    /// <summary>Allows viewing the audit log.</summary>
    ViewAuditLog = 1 << 22,

    /// <summary>Allows managing supplier records.</summary>
    ManageSuppliers = 1 << 23,

    /// <summary>Allows managing purchase orders.</summary>
    ManagePurchases = 1 << 24,

    /// <summary>Allows managing customer records.</summary>
    ManageCustomers = 1 << 25,

    /// <summary>Allows managing promotions (create, edit, activate/deactivate).</summary>
    ManagePromotions = 1 << 26
}