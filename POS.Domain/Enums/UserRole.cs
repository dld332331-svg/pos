namespace POS.Domain.Enums;

/// <summary>
/// Defines the roles available to users in the POS system.
/// Each role carries a predefined set of permissions.
/// </summary>
public enum UserRole
{
    /// <summary>Full access to all system features. No restrictions.</summary>
    Admin,

    /// <summary>Access to management features including reports, inventory, and user oversight.</summary>
    Manager,

    /// <summary>Access to sales operations, basic item cancellation, and cash drawer.</summary>
    Cashier,

    /// <summary>Access to kitchen display and order status management.</summary>
    Kitchen
}