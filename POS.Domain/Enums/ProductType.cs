namespace POS.Domain.Enums;

/// <summary>
/// Specifies how a product is sold and measured.
/// </summary>
public enum ProductType
{
    /// <summary>Standard unit-based product sold by quantity.</summary>
    Standard,

    /// <summary>Product sold by weight (e.g., per kg).</summary>
    Weighted,

    /// <summary>Product used as a modifier/add-on for other products.</summary>
    Modifier
}