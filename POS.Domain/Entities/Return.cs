namespace POS.Domain.Entities;

public class Return : BaseEntity
{
    public string? ReturnNumber { get; set; }
    public Guid OriginalSaleId { get; set; }
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public Guid? CustomerId { get; set; }
    public Sale? Sale { get; set; }
    public User? User { get; set; }
    public Customer? Customer { get; set; }

    private readonly List<ReturnItem> _items = new();
    public IReadOnlyCollection<ReturnItem> Items => _items.AsReadOnly();

    public void AddItem(ReturnItem item) => _items.Add(item);
    public void RemoveItem(ReturnItem item) => _items.Remove(item);
}
