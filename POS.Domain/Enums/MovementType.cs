namespace POS.Domain.Enums;

/// <summary>
/// Specifies the type of inventory movement being recorded.
/// </summary>
public enum MovementType
{
    /// <summary>Stock received from a supplier via purchase order.</summary>
    Purchase,

    /// <summary>Stock reduced due to a sale transaction.</summary>
    Sale,

    /// <summary>Stock increased due to a customer return.</summary>
    Return,

    /// <summary>Stock removed due to spoilage, damage, or expiration.</summary>
    Waste,

    /// <summary>Manual correction of inventory quantity.</summary>
    Adjustment,

    /// <summary>Stock moved between locations or registers.</summary>
    Transfer,

    /// <summary>Periodic physical stock count performed for reconciliation.</summary>
    StockCount
}