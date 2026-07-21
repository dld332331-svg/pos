using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class InventoryMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public MovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal BeforeQuantity { get; set; }
    public decimal AfterQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? InventoryBatchId { get; set; }
    public string? Reference { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public InventoryItem? InventoryItem { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public User? User { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Sale? Sale { get; set; }
}
