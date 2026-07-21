namespace POS.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a product.
/// </summary>
public enum ProductStatus
{
    /// <summary>Product is active and available for sale.</summary>
    Active,

    /// <summary>Product is temporarily unavailable (e.g., out of stock seasonally).</summary>
    Inactive,

    /// <summary>Product has been archived and is no longer maintained.</summary>
    Archived
}