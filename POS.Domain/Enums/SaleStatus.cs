namespace POS.Domain.Enums;

/// <summary>
/// Represents the current status of a sale/invoice in the POS system.
/// </summary>
public enum SaleStatus
{
    /// <summary>Sale is currently being processed.</summary>
    Active,

    /// <summary>Sale has been fully paid and completed.</summary>
    Completed,

    /// <summary>Sale has been cancelled.</summary>
    Cancelled,

    /// <summary>Sale has been placed on hold for later completion.</summary>
    Held,

    /// <summary>Sale has been returned (full or partial refund).</summary>
    Returned
}