namespace POS.Domain.Enums;

/// <summary>
/// Specifies whether a cash movement is a withdrawal or deposit.
/// </summary>
public enum WithdrawalDepositType
{
    /// <summary>Cash removed from the register during a shift.</summary>
    Withdrawal,

    /// <summary>Cash added to the register during a shift.</summary>
    Deposit
}