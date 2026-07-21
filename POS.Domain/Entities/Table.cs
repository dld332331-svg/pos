using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Table : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public Guid RoomId { get; set; }
    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public Guid? CurrentOrderId { get; set; }

    // Navigation
    public Room? Room { get; set; }
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
