namespace POS.Domain.Entities;

public class Room : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<Table> Tables { get; set; } = new List<Table>();
}
