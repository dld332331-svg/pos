using Xunit;
using POS.Domain.ValueObjects;
using FluentAssertions;

namespace POS.Tests.UnitTests;

/// <summary>
/// Comprehensive unit tests for the Money value object.
/// Covers creation, validation, equality, comparison, operators,
/// hashing, and edge cases to close the 12 uncovered branches.
/// </summary>
public sealed class MoneyTests
{
    // ========================================================================
    // Creation — Factory Methods
    // ========================================================================

    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var zero = Money.Zero();
        zero.Amount.Should().Be(0m);
    }

    [Fact]
    public void Zero_ToString_ShouldBeZeroDotThreeZeros()
    {
        Money.Zero().ToString().Should().Be("0.000");
    }

    [Fact]
    public void FromDecimal_ShouldCreateMoneyWithRoundedAmount()
    {
        var money = Money.FromDecimal(1.12345m);
        money.Amount.Should().Be(1.123m);
    }

    [Fact]
    public void FromDecimal_RoundsUp_AtMidpoint()
    {
        var money = Money.FromDecimal(1.1235m);
        money.Amount.Should().Be(1.124m);
    }

    [Fact]
    public void FromDecimal_AlreadyRounded_ShouldPreserve()
    {
        var money = Money.FromDecimal(5.000m);
        money.Amount.Should().Be(5.000m);
    }

    [Fact]
    public void FromDecimal_NegativeValue_ShouldAccept()
    {
        var money = Money.FromDecimal(-10.500m);
        money.Amount.Should().Be(-10.500m);
    }

    [Fact]
    public void FromDecimal_VerySmallValue_ShouldRoundToZero()
    {
        var money = Money.FromDecimal(0.0004m);
        money.Amount.Should().Be(0.000m);
    }

    // ========================================================================
    // Validate — Boundary Checks
    // ========================================================================

    [Fact]
    public void Validate_MaxAllowedValue_ShouldSucceed()
    {
        var money = Money.FromDecimal(999_999_999.999m);
        money.Amount.Should().Be(999_999_999.999m);
    }

    [Fact]
    public void Validate_MinAllowedValue_ShouldSucceed()
    {
        var money = Money.FromDecimal(-999_999_999.999m);
        money.Amount.Should().Be(-999_999_999.999m);
    }

    [Fact]
    public void Validate_ExceedsMax_ShouldThrowInvalidOperationException()
    {
        var act = () => Money.FromDecimal(1_000_000_000.000m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the valid range*");
    }

    [Fact]
    public void Validate_ExceedsMaxByLargeMargin_ShouldThrow()
    {
        var act = () => Money.FromDecimal(999_999_999_999.999m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the valid range*");
    }

    [Fact]
    public void Validate_BelowMin_ShouldThrowInvalidOperationException()
    {
        var act = () => Money.FromDecimal(-1_000_000_000.000m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the valid range*");
    }

    // ========================================================================
    // ToString
    // ========================================================================

    [Fact]
    public void ToString_FormatAs3DecimalPlaces()
    {
        var money = Money.FromDecimal(1.5m);
        money.ToString().Should().Be("1.500");
    }

    [Fact]
    public void ToString_Zero_ShouldBe0_000()
    {
        Money.Zero().ToString().Should().Be("0.000");
    }

    [Fact]
    public void ToString_Negative_ShouldIncludeMinusSign()
    {
        var money = Money.FromDecimal(-5.500m);
        money.ToString().Should().Be("-5.500");
    }

    [Fact]
    public void ToString_LargeNumber_ShouldFormatCorrectly()
    {
        var money = Money.FromDecimal(123456.789m);
        money.ToString().Should().Be("123456.789");
    }

    // ========================================================================
    // Equals(Money?) — IEquatable<Money>
    // ========================================================================

    [Fact]
    public void Equals_SameAmount_ShouldReturnTrue()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.123m);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentAmount_ShouldReturnFalse()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.124m);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var money = Money.FromDecimal(5.123m);

        money.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameReference_ShouldReturnTrue()
    {
        var money = Money.FromDecimal(5.123m);

        money.Equals(money).Should().BeTrue();
    }

    [Fact]
    public void Equals_NegativeSameAmount_ShouldReturnTrue()
    {
        var a = Money.FromDecimal(-10.000m);
        var b = Money.FromDecimal(-10.000m);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_ZeroAndZero_ShouldReturnTrue()
    {
        var a = Money.Zero();
        var b = Money.FromDecimal(0m);

        a.Equals(b).Should().BeTrue();
    }

    // ========================================================================
    // Equals(object?)
    // ========================================================================

    [Fact]
    public void ObjectEquals_SameAmount_ShouldReturnTrue()
    {
        object a = Money.FromDecimal(5.123m);
        object b = Money.FromDecimal(5.123m);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ObjectEquals_DifferentAmount_ShouldReturnFalse()
    {
        object a = Money.FromDecimal(5.123m);
        object b = Money.FromDecimal(5.124m);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_Null_ShouldReturnFalse()
    {
        object money = Money.FromDecimal(5.123m);

        money.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_DifferentType_ShouldReturnFalse()
    {
        var money = Money.FromDecimal(5.123m);
        var notMoney = "5.123";

        money.Equals(notMoney).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_SameReference_ShouldReturnTrue()
    {
        object money = Money.FromDecimal(5.123m);

        money.Equals(money).Should().BeTrue();
    }

    // ========================================================================
    // CompareTo(Money?) — IComparable<Money>
    // ========================================================================

    [Fact]
    public void CompareTo_Null_ShouldReturnPositive()
    {
        var money = Money.FromDecimal(5.000m);

        money.CompareTo(null).Should().BePositive();
    }

    [Fact]
    public void CompareTo_SameAmount_ShouldReturnZero()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(5.000m);

        a.CompareTo(b).Should().Be(0);
    }

    [Fact]
    public void CompareTo_GreaterAmount_ShouldReturnPositive()
    {
        var a = Money.FromDecimal(10.000m);
        var b = Money.FromDecimal(5.000m);

        a.CompareTo(b).Should().BePositive();
    }

    [Fact]
    public void CompareTo_LesserAmount_ShouldReturnNegative()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(10.000m);

        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_NegativeVsPositive_ShouldReturnNegative()
    {
        var a = Money.FromDecimal(-5.000m);
        var b = Money.FromDecimal(5.000m);

        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_ZeroAndZero_ShouldReturnZero()
    {
        var a = Money.Zero();
        var b = Money.FromDecimal(0m);

        a.CompareTo(b).Should().Be(0);
    }

    // ========================================================================
    // GetHashCode
    // ========================================================================

    [Fact]
    public void GetHashCode_SameAmount_ShouldBeEqual()
    {
        var h1 = Money.FromDecimal(5.123m).GetHashCode();
        var h2 = Money.FromDecimal(5.123m).GetHashCode();

        h1.Should().Be(h2);
    }

    [Fact]
    public void GetHashCode_DifferentAmount_ShouldDiffer()
    {
        var h1 = Money.FromDecimal(5.123m).GetHashCode();
        var h2 = Money.FromDecimal(5.124m).GetHashCode();

        h1.Should().NotBe(h2);
    }

    [Fact]
    public void GetHashCode_Zero_ShouldBeConsistent()
    {
        var h1 = Money.Zero().GetHashCode();
        var h2 = Money.Zero().GetHashCode();

        h1.Should().Be(h2);
    }

    // ========================================================================
    // Operator ==
    // ========================================================================

    [Fact]
    public void OperatorEquals_BothNull_ShouldReturnTrue()
    {
        Money? a = null;
        Money? b = null;

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_LeftNull_ShouldReturnFalse()
    {
        Money? a = null;
        var b = Money.FromDecimal(5.000m);

        (a == b).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_RightNull_ShouldReturnFalse()
    {
        var a = Money.FromDecimal(5.000m);
        Money? b = null;

        (a == b).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_SameAmount_ShouldReturnTrue()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.123m);

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_DifferentAmount_ShouldReturnFalse()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.124m);

        (a == b).Should().BeFalse();
    }

    // ========================================================================
    // Operator !=
    // ========================================================================

    [Fact]
    public void OperatorNotEquals_BothNull_ShouldReturnFalse()
    {
        Money? a = null;
        Money? b = null;

        (a != b).Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_SameAmount_ShouldReturnFalse()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.123m);

        (a != b).Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_DifferentAmount_ShouldReturnTrue()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.124m);

        (a != b).Should().BeTrue();
    }

    // ========================================================================
    // Operator +
    // ========================================================================

    [Fact]
    public void OperatorAddition_ShouldReturnCorrectSum()
    {
        var a = Money.FromDecimal(1.500m);
        var b = Money.FromDecimal(2.250m);

        var result = a + b;

        result.Amount.Should().Be(3.750m);
    }

    [Fact]
    public void OperatorAddition_WithNegative_ShouldSubtract()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(-2.000m);

        var result = a + b;

        result.Amount.Should().Be(3.000m);
    }

    [Fact]
    public void OperatorAddition_WithZero_ShouldPreserve()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.Zero();

        var result = a + b;

        result.Amount.Should().Be(5.000m);
    }

    [Fact]
    public void OperatorAddition_NullLeft_ShouldThrow()
    {
        Money nullLeft = null!;
        var act = () => { var _ = nullLeft + Money.FromDecimal(1.000m); };
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OperatorAddition_NullRight_ShouldThrow()
    {
        Money nullRight = null!;
        var act = () => { var _ = Money.FromDecimal(1.000m) + nullRight; };
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // Operator -
    // ========================================================================

    [Fact]
    public void OperatorSubtraction_ShouldReturnCorrectDifference()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(2.500m);

        var result = a - b;

        result.Amount.Should().Be(2.500m);
    }

    [Fact]
    public void OperatorSubtraction_NegativeResult_ShouldBeNegative()
    {
        var a = Money.FromDecimal(2.000m);
        var b = Money.FromDecimal(5.000m);

        var result = a - b;

        result.Amount.Should().Be(-3.000m);
    }

    [Fact]
    public void OperatorSubtraction_NullLeft_ShouldThrow()
    {
        Money nullLeft = null!;
        var act = () => { var _ = nullLeft - Money.FromDecimal(1.000m); };
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OperatorSubtraction_NullRight_ShouldThrow()
    {
        Money nullRight = null!;
        var act = () => { var _ = Money.FromDecimal(1.000m) - nullRight; };
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // Operator * (Money * decimal)
    // ========================================================================

    [Fact]
    public void OperatorMultiply_MoneyByScalar_ShouldReturnCorrectProduct()
    {
        var money = Money.FromDecimal(10.000m);

        var result = money * 0.16m;

        result.Amount.Should().Be(1.600m);
    }

    [Fact]
    public void OperatorMultiply_MoneyByZero_ShouldReturnZero()
    {
        var money = Money.FromDecimal(10.000m);

        var result = money * 0m;

        result.Amount.Should().Be(0.000m);
    }

    [Fact]
    public void OperatorMultiply_MoneyByNegative_ShouldReturnNegative()
    {
        var money = Money.FromDecimal(10.000m);

        var result = money * -1m;

        result.Amount.Should().Be(-10.000m);
    }

    [Fact]
    public void OperatorMultiply_NullMoney_ShouldThrow()
    {
        Money nullMoney = null!;
        var act = () => { var _ = nullMoney * 2m; };
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // Operator * (decimal * Money)
    // ========================================================================

    [Fact]
    public void OperatorMultiply_ScalarByMoney_ShouldReturnCorrectProduct()
    {
        var money = Money.FromDecimal(10.000m);

        var result = 0.16m * money;

        result.Amount.Should().Be(1.600m);
    }

    [Fact]
    public void OperatorMultiply_ScalarByNullMoney_ShouldThrow()
    {
        Money nullMoney = null!;
        var act = () => { var _ = 2m * nullMoney; };
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // Equality Contract (Equals + GetHashCode consistency)
    // ========================================================================

    [Fact]
    public void EqualsAndGetHashCode_EqualValues_ShouldHaveEqualHashes()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.123m);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void EqualsAndGetHashCode_DifferentValues_ShouldDiffer()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.124m);

        a.Equals(b).Should().BeFalse();
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    // ========================================================================
    // CompareTo + Equality Consistency
    // ========================================================================

    [Fact]
    public void CompareTo_Zero_Equals_IsTrue_Correlation()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(5.000m);

        a.CompareTo(b).Should().Be(0);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_NonZero_Equals_IsFalse_Correlation()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(5.001m);

        a.CompareTo(b).Should().NotBe(0);
        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
    }

    // ========================================================================
    // Immutability
    // ========================================================================

    [Fact]
    public void Amount_Property_ShouldBeImmutable()
    {
        var money = Money.FromDecimal(5.000m);

        // Amount has only a getter (no setter)
        var type = money.GetType();
        var property = type.GetProperty("Amount")!;
        property.CanWrite.Should().BeFalse();
        property.CanRead.Should().BeTrue();
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public void FromDecimal_MaxPrecision_ShouldRoundCorrectly()
    {
        // 4 decimal places should round to 3
        var money = Money.FromDecimal(1.2345m);
        money.Amount.Should().Be(1.235m); // AwayFromZero rounding
    }

    [Fact]
    public void FromDecimal_MinPrecision_OneDecimal_ShouldPad()
    {
        var money = Money.FromDecimal(0.1m);
        money.Amount.Should().Be(0.100m);
    }

    [Fact]
    public void CompareTo_DifferentSigns_ShouldWork()
    {
        var positive = Money.FromDecimal(1.000m);
        var negative = Money.FromDecimal(-1.000m);

        positive.CompareTo(negative).Should().BePositive();
        negative.CompareTo(positive).Should().BeNegative();
        positive.CompareTo(Money.Zero()).Should().BePositive();
        negative.CompareTo(Money.Zero()).Should().BeNegative();
    }

    [Fact]
    public void OperatorAddition_Rounding_ShouldRoundTo3Decimals()
    {
        var a = Money.FromDecimal(1.000m);
        var b = Money.FromDecimal(0.0005m);

        var result = a + b;

        result.Amount.Should().Be(1.001m);
    }
}
