namespace POS.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public Guid UserId { get; set; }

    // Navigation
    public Supplier? Supplier { get; set; }
    public User? User { get; set; }
    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();
    public void AddItem(PurchaseOrderItem item) => _items.Add(item);
    public void RemoveItem(PurchaseOrderItem item) => _items.Remove(item);
}
