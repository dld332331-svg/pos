namespace POS.Domain.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ReceivedQuantity { get; set; }

    // Navigation
    public PurchaseOrder? PurchaseOrder { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
