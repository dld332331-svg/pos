namespace POS.Domain.BusinessRules;

/// <summary>
/// Provides static methods for financial calculations in the POS system.
/// All monetary values use Jordanian Dinar (JOD) with 3 decimal places.
/// Every financial calculation in the system MUST go through this policy
/// to ensure consistent rounding behavior.
/// </summary>
public static class MoneyPolicy
{
    /// <summary>
    /// The number of decimal places used for JOD currency.
    /// </summary>
    public const int JODDecimalPlaces = 3;

    /// <summary>
    /// Rounds a decimal value to 3 decimal places using MidpointRounding.AwayFromZero.
    /// This is the standard rounding method for Jordanian Dinars.
    /// </summary>
    /// <param name="value">The decimal value to round.</param>
    /// <returns>The value rounded to 3 decimal places.</returns>
    public static decimal RoundToJOD(decimal value)
    {
        return Math.Round(value, JODDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}