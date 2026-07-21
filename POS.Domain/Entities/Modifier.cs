namespace POS.Domain.Entities;

public class Modifier : BaseEntity
{
    public Guid ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ModifierGroup? ModifierGroup { get; set; }
    public ICollection<ModifierSize> Sizes { get; set; } = new List<ModifierSize>();
}
