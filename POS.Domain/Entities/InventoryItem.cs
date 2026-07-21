namespace POS.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public string? Unit { get; set; }
    public decimal Cost { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
    public decimal MinQuantity { get; set; }
    public decimal MaxQuantity { get; set; }
    public Guid ProductId { get; set; }
    public Guid? SupplierId { get; set; }

    // Navigation
    public Supplier? Supplier { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
    public ICollection<InventoryBatch> Batches { get; set; } = new List<InventoryBatch>();
}
