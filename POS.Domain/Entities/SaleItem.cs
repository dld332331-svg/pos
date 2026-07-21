namespace POS.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductArabicName { get; set; }
    public Guid? KitchenStationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
    public string? ModifierSummary { get; set; }

    // ===== Unit-of-Measure Support (P1 enhancement) =====
    /// <summary>
    /// The unit-of-measure selected by the cashier at POS for display purposes.
    /// Null means the product's default unit was used. This is the FK to UnitOfMeasure.
    /// Only used for display/retrieval — inventory is always tracked in the product's
    /// default unit via the Quantity field.
    /// </summary>
    public Guid? UnitOfMeasureId { get; set; }

    /// <summary>
    /// The quantity expressed in the display unit (e.g., 500 for "g" when default is kg).
    /// Unlike Quantity (which is always in the product's default unit), this field stores
    /// the value the cashier actually entered. Null means same as Quantity.
    /// </summary>
    public decimal? DisplayQuantity { get; set; }

    // Navigation
    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
    public KitchenStation? KitchenStation { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    private readonly List<SaleItemModifier> _modifiers = new();
    public IReadOnlyCollection<SaleItemModifier> Modifiers => _modifiers.AsReadOnly();
    public void AddModifier(SaleItemModifier modifier) => _modifiers.Add(modifier);
}
