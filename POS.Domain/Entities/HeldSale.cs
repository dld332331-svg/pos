namespace POS.Domain.Entities;

public class HeldSale : BaseEntity
{
    public string SerializedData { get; set; } = string.Empty;
    public Guid ShiftId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TableId { get; set; }
    public string? HoldReason { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public User? User { get; set; }
    public Table? Table { get; set; }
    public Shift? Shift { get; set; }
}
