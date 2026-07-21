using Xunit;
using POS.Domain.BusinessRules;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class MoneyPolicyTests
{
    [Fact]
    public void RoundToJOD_ShouldRoundTo3DecimalPlaces()
    {
        MoneyPolicy.RoundToJOD(1.1234m).Should().Be(1.123m);
        MoneyPolicy.RoundToJOD(1.1235m).Should().Be(1.124m);
        MoneyPolicy.RoundToJOD(0.0005m).Should().Be(0.001m);
        MoneyPolicy.RoundToJOD(12.3754m).Should().Be(12.375m);
        MoneyPolicy.RoundToJOD(12.3755m).Should().Be(12.376m);
    }

    [Fact]
    public void RoundToJOD_ExactValue_ShouldNotChange()
    {
        MoneyPolicy.RoundToJOD(1.000m).Should().Be(1.000m);
        MoneyPolicy.RoundToJOD(0.250m).Should().Be(0.250m);
        MoneyPolicy.RoundToJOD(99.999m).Should().Be(99.999m);
    }

    [Fact]
    public void RoundToJOD_Zero_ShouldReturnZero()
    {
        MoneyPolicy.RoundToJOD(0m).Should().Be(0m);
        MoneyPolicy.RoundToJOD(0.0001m).Should().Be(0.000m);
    }

    [Fact]
    public void RoundToJOD_LargeValues_ShouldRoundCorrectly()
    {
        MoneyPolicy.RoundToJOD(99999.9995m).Should().Be(100000.000m);
        MoneyPolicy.RoundToJOD(12345.6789m).Should().Be(12345.679m);
    }

    [Fact]
    public void RoundToJOD_NegativeValues_ShouldRoundCorrectly()
    {
        MoneyPolicy.RoundToJOD(-1.1235m).Should().Be(-1.124m);
        MoneyPolicy.RoundToJOD(-0.0004m).Should().Be(0.000m);
    }
}