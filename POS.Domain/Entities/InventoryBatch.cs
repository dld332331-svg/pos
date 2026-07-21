namespace POS.Domain.Entities;

public class InventoryBatch : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public Guid? SupplierId { get; set; }

    // Navigation
    public InventoryItem? InventoryItem { get; set; }
    public Supplier? Supplier { get; set; }
}
