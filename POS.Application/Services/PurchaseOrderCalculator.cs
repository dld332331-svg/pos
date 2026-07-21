using POS.Domain.BusinessRules;

namespace POS.Application.Services;

/// <summary>
/// Pure computation helper for purchase order business logic.
/// Keeps line cost calculation and status transitions out of the UI layer.
/// </summary>
public static class PurchaseOrderCalculator
{
    /// <summary>
    /// Computes the line cost for a purchase order item with proper rounding.
    /// Formula: Round(quantity * unitCost, 3) to match JOD three-decimal precision.
    /// </summary>
    public static decimal ComputeLineCost(decimal quantity, decimal unitCost)
    {
        return MoneyPolicy.RoundToJOD(quantity * unitCost);
    }

    /// <summary>
    /// Computes the total cost across all purchase order item DTOs.
    /// </summary>
    public static decimal ComputeTotalCost(IEnumerable<PurchaseOrderItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Sum(i => ComputeLineCost(i.Quantity, i.UnitCost));
    }

    /// <summary>
    /// Checks whether a status transition is allowed in the PO workflow.
    /// Workflow: Pending ↔ PartiallyReceived ↔ Received (terminal)
    ///          Pending ↔ Cancelled (terminal)
    ///          PartiallyReceived ↔ Cancelled (terminal)
    /// </summary>
    public static bool IsValidTransition(string currentStatus, string newStatus)
    {
        if (string.IsNullOrEmpty(currentStatus) || string.IsNullOrEmpty(newStatus))
            return false;

        return (currentStatus, newStatus) switch
        {
            ("Pending", "PartiallyReceived") => true,
            ("Pending", "Received") => true,
            ("Pending", "Cancelled") => true,
            ("PartiallyReceived", "Received") => true,
            ("PartiallyReceived", "Cancelled") => true,
            (_, "Pending") => false, // Never go back to pending
            ("Received", _) => false, // Terminal state
            ("Cancelled", _) => false, // Terminal state
            _ => false
        };
    }

    /// <summary>
    /// Returns the Arabic display text for a PO status.
    /// </summary>
    public static string GetStatusDisplayText(string status) => status switch
    {
        "Pending" => "جديد",
        "PartiallyReceived" => "مستلم جزئياً",
        "Received" => "مستلم",
        "Cancelled" => "ملغي",
        _ => "غير معروف"
    };

    /// <summary>
    /// Returns the color name for a PO status (for UI formatting).
    /// </summary>
    public static string GetStatusColorName(string status) => status switch
    {
        "Pending" => "Info",
        "PartiallyReceived" => "Warning",
        "Received" => "Success",
        "Cancelled" => "Error",
        _ => "TextPrimary"
    };

    /// <summary>
    /// Computes the remaining quantity to be received.
    /// </summary>
    public static int ComputeRemainingQuantity(int orderedQuantity, int receivedQuantity)
    {
        return Math.Max(0, orderedQuantity - receivedQuantity);
    }
}
