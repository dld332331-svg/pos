namespace POS.Domain.Entities;

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "piece";

    // Navigation
    public Recipe? Recipe { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
