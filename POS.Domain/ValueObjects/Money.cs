using POS.Domain.BusinessRules;

namespace POS.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in Jordanian Dinars (JOD).
/// All amounts are stored with exactly 3 decimal places as per JOD currency standards.
/// </summary>
public sealed class Money : IEquatable<Money>, IComparable<Money>
{
    /// <summary>
    /// The monetary amount in JOD, rounded to 3 decimal places.
    /// </summary>
    public decimal Amount { get; }

    private Money(decimal amount)
    {
        Amount = MoneyPolicy.RoundToJOD(amount);
        Validate();
    }

    /// <summary>
    /// Validates that the amount is within acceptable range.
    /// Throws <see cref="InvalidOperationException"/> if invalid.
    /// </summary>
    public void Validate()
    {
        if (Amount is < -999_999_999.999m or > 999_999_999.999m)
        {
            throw new InvalidOperationException(
                $"Money amount {Amount} is outside the valid range for JOD currency. " +
                "Maximum supported value is 999,999,999.999 JOD.");
        }
    }

    /// <summary>
    /// Returns the amount formatted with exactly 3 decimal places (e.g., "1.500").
    /// </summary>
    public override string ToString() => Amount.ToString("0.000");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(Money? other)
    {
        if (other is null) return false;
        return Amount == other.Amount;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Amount.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        return Amount.CompareTo(other.Amount);
    }

    // --- Operators ---

    /// <summary>Adds two monetary amounts.</summary>
    public static Money operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new(left.Amount + right.Amount);
    }

    /// <summary>Subtracts the right amount from the left.</summary>
    public static Money operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return new(left.Amount - right.Amount);
    }

    /// <summary>Multiplies a monetary amount by a scalar factor.</summary>
    public static Money operator *(Money money, decimal multiplier)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new(money.Amount * multiplier);
    }

    /// <summary>Multiplies a scalar factor by a monetary amount.</summary>
    public static Money operator *(decimal multiplier, Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new(multiplier * money.Amount);
    }

    /// <summary>Returns true if both monetary amounts are equal.</summary>
    public static bool operator ==(Money? left, Money? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    /// <summary>Returns true if the monetary amounts are not equal.</summary>
    public static bool operator !=(Money? left, Money? right) => !(left == right);

    // --- Static Factory Methods ---

    /// <summary>
    /// Creates a Money instance representing zero JOD (0.000).
    /// </summary>
    public static Money Zero() => new(0m);

    /// <summary>
    /// Creates a Money instance from a decimal value, rounded to 3 decimal places
    /// using <see cref="MidpointRounding.AwayFromZero"/>.
    /// </summary>
    /// <param name="amount">The decimal amount in JOD.</param>
    /// <returns>A new Money instance with the rounded amount.</returns>
    public static Money FromDecimal(decimal amount) => new(amount);
}