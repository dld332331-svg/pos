namespace POS.Domain.Enums;

/// <summary>
/// Specifies the type of action recorded in the audit log.
/// Each value represents a distinct auditable event in the system.
/// </summary>
public enum AuditActionType
{
    /// <summary>User successfully logged in.</summary>
    LoginSuccess,

    /// <summary>Failed login attempt.</summary>
    LoginFailure,

    /// <summary>User logged out.</summary>
    Logout,

    /// <summary>Product or sale price was changed.</summary>
    PriceChange,

    /// <summary>A discount was applied to a sale or item.</summary>
    DiscountApplied,

    /// <summary>A return was processed.</summary>
    ReturnProcessed,

    /// <summary>A sale or item was cancelled.</summary>
    CancellationProcessed,

    /// <summary>Inventory quantity was adjusted.</summary>
    InventoryAdjusted,

    /// <summary>Waste was recorded for inventory items.</summary>
    WasteRecorded,

    /// <summary>A physical stock count was performed.</summary>
    StockCount,

    /// <summary>A new product was created.</summary>
    ProductCreated,

    /// <summary>An existing product was updated.</summary>
    ProductUpdated,

    /// <summary>A product was archived.</summary>
    ProductArchived,

    /// <summary>A new user account was created.</summary>
    UserCreated,

    /// <summary>An existing user account was updated.</summary>
    UserUpdated,

    /// <summary>A user account was deleted.</summary>
    UserDeleted,

    /// <summary>Permissions were changed for a user or role.</summary>
    PermissionChanged,

    /// <summary>A system setting was changed.</summary>
    SettingChanged,

    /// <summary>A data backup was performed.</summary>
    BackupPerformed,

    /// <summary>A data restore was performed.</summary>
    RestorePerformed,

    /// <summary>A printer configuration was changed.</summary>
    PrinterConfigChanged,

    /// <summary>A product recipe was changed.</summary>
    RecipeChanged,

    /// <summary>A sale was completed.</summary>
    SaleCompleted,

    /// <summary>A payment was processed.</summary>
    PaymentProcessed,

    /// <summary>A cash register shift was opened.</summary>
    ShiftOpened,

    /// <summary>A cash register shift was closed.</summary>
    ShiftClosed
}