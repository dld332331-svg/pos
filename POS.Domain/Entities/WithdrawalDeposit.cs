using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class WithdrawalDeposit : BaseEntity
{
    public Guid ShiftId { get; set; }
    public decimal Amount { get; set; }
    public WithdrawalDepositType Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ArabicReason { get; set; }
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Shift? Shift { get; set; }
}
