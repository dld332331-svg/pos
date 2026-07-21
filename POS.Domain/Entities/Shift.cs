using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Shift : BaseEntity
{
    public int ShiftNumber { get; set; }
    public Guid UserId { get; set; }
    public Guid RegisterId { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal OtherSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal Difference { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? Variance { get; set; }
    public string? Notes { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public Register? Register { get; set; }
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<WithdrawalDeposit> WithdrawalDeposits { get; set; } = new List<WithdrawalDeposit>();
}
