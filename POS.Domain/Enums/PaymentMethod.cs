namespace POS.Domain.Enums;

/// <summary>
/// Specifies the method of payment for a sale.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Payment made with cash.</summary>
    Cash,

    /// <summary>Payment made via credit/debit card or electronic payment terminal.</summary>
    Card,

    /// <summary>Payment split across multiple payment methods.</summary>
    Multiple,

    /// <summary>Payment via e-wallet / mobile wallet.</summary>
    EWallet,

    /// <summary>Credit sale (deferred payment).</summary>
    Credit
}