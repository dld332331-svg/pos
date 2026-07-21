namespace POS.Domain.Enums;

/// <summary>
/// Defines the functional role of a printer in the POS system.
/// Determines what type of documents the printer produces.
/// </summary>
public enum PrinterRole
{
    /// <summary>Prints customer receipts at the point of sale.</summary>
    Receipt,

    /// <summary>Prints kitchen orders for food preparation.</summary>
    Kitchen,

    /// <summary>Prints beverage orders for the bar area.</summary>
    Beverage,

    /// <summary>Prints department-specific orders or reports.</summary>
    Department
}