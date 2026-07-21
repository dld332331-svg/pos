using Xunit;
using POS.Domain.ValueObjects;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var zero = Money.Zero();
        zero.Amount.Should().Be(0m);
    }

    [Fact]
    public void FromDecimal_ShouldCreateMoneyWithRoundedAmount()
    {
        var money = Money.FromDecimal(1.12345m);
        money.Amount.Should().Be(1.123m);
    }

    [Fact]
    public void Addition_ShouldReturnCorrectSum()
    {
        var a = Money.FromDecimal(1.500m);
        var b = Money.FromDecimal(2.250m);
        var result = a + b;
        result.Amount.Should().Be(3.750m);
    }

    [Fact]
    public void Subtraction_ShouldReturnCorrectDifference()
    {
        var a = Money.FromDecimal(5.000m);
        var b = Money.FromDecimal(2.500m);
        var result = a - b;
        result.Amount.Should().Be(2.500m);
    }

    [Fact]
    public void Multiplication_ShouldReturnCorrectProduct()
    {
        var money = Money.FromDecimal(10.000m);
        var result = money * 0.16m;
        result.Amount.Should().Be(1.600m);
    }

    [Fact]
    public void ToString_ShouldFormatAs3DecimalPlaces()
    {
        var money = Money.FromDecimal(1.5m);
        money.ToString().Should().Be("1.500");
    }

    [Fact]
    public void Equality_SameAmount_ShouldBeEqual()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.123m);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentAmount_ShouldNotBeEqual()
    {
        var a = Money.FromDecimal(5.123m);
        var b = Money.FromDecimal(5.124m);
        (a != b).Should().BeTrue();
    }
}