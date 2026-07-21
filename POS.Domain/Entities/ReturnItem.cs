namespace POS.Domain.Entities;

public class ReturnItem : BaseEntity
{
    public Guid ReturnId { get; set; }
    public Guid SaleItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductArabicName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal ReturnAmount { get; set; }
    public string? Reason { get; set; }

    // Navigation
    public Return? Return { get; set; }
}
