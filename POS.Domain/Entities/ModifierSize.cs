namespace POS.Domain.Entities;

public class ModifierSize : BaseEntity
{
    public Guid ModifierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public decimal Price { get; set; }
    public decimal PriceAdjustment { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public Modifier? Modifier { get; set; }
}
