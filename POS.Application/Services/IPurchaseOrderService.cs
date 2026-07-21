using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Service for managing purchase orders to suppliers.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Creates a new purchase order.
    /// </summary>
    Task<PurchaseOrderDto> CreatePurchaseOrderAsync(Guid supplierId, Guid userId, List<PurchaseOrderItemDto> items, string? notes);

    /// <summary>
    /// Gets a purchase order by ID.
    /// </summary>
    Task<PurchaseOrderDto?> GetPurchaseOrderAsync(Guid purchaseOrderId);

    /// <summary>
    /// Gets all purchase orders with optional status filter.
    /// </summary>
    Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(string? status = null);

    /// <summary>
    /// Updates the status of a purchase order (e.g., Pending → Received → Cancelled).
    /// </summary>
    Task<OperationResult> UpdatePurchaseOrderStatusAsync(Guid purchaseOrderId, string status);

    /// <summary>
    /// Marks a purchase order as received and updates inventory.
    /// </summary>
    Task<OperationResult> ReceivePurchaseOrderAsync(Guid purchaseOrderId, Guid userId);
}

public record PurchaseOrderDto(
    Guid Id, string OrderNumber, string SupplierName,
    decimal TotalAmount, string Status, DateTime CreatedAt,
    List<PurchaseOrderItemDto> Items, string? Notes);

public record PurchaseOrderItemDto(
    Guid InventoryItemId, string ItemName,
    decimal Quantity, decimal UnitCost, decimal TotalCost, decimal ReceivedQuantity);
