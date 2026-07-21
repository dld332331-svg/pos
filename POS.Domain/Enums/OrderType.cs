namespace POS.Domain.Enums;

/// <summary>
/// Specifies how an order is served to the customer.
/// </summary>
public enum OrderType
{
    /// <summary>Customer dines inside the establishment. Associated with a table.</summary>
    DineIn,

    /// <summary>Customer takes the order away. No table assignment.</summary>
    Takeaway,

    /// <summary>Order is delivered to the customer's address.</summary>
    Delivery
}