namespace POS.Domain.Enums;

/// <summary>
/// Represents the current state of a restaurant table.
/// </summary>
public enum TableStatus
{
    /// <summary>Table is free and available for new customers.</summary>
    Available,

    /// <summary>Table is currently occupied by customers with an active order.</summary>
    Occupied,

    /// <summary>Table's order is being prepared in the kitchen.</summary>
    Preparing,

    /// <summary>Table's order is ready to be served.</summary>
    Ready,

    /// <summary>Table's order has been served and customer is awaiting payment.</summary>
    WaitingForPayment,

    /// <summary>Table has been reserved for a future time slot.</summary>
    Reserved,

    /// <summary>Table is being cleaned after use.</summary>
    Cleaning
}