namespace POS.Domain.Entities;

public class SaleItemModifier : BaseEntity
{
    public Guid SaleItemId { get; set; }
    public Guid ModifierId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public string? ModifierArabicName { get; set; }
    public string? SizeName { get; set; }
    public decimal Price { get; set; }
    public decimal AdditionalPrice { get; set; }
    public decimal Quantity { get; set; }

    // Navigation
    public SaleItem? SaleItem { get; set; }
}
