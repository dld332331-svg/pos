using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Guid CategoryId { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Standard;
    public string Unit { get; set; } = "piece";
    public Guid? UnitOfMeasureId { get; set; }
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; }
    public decimal MinStock { get; set; }
    public Guid? SupplierId { get; set; }
    public string? ImagePath { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public Guid? KitchenStationId { get; set; }
    public bool AllowModifiers { get; set; }
    public string? SearchTerms { get; set; }

    // Navigation
    public Category? Category { get; set; }
    public KitchenStation? KitchenStation { get; set; }
    public Recipe? Recipe { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
}
