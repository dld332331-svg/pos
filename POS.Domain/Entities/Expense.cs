namespace POS.Domain.Entities;

public class Expense : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public string? ArabicDescription { get; set; }
    public decimal Amount { get; set; }
    public Guid UserId { get; set; }
    public Guid ShiftId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Shift? Shift { get; set; }
}
