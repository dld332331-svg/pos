using Xunit;
using POS.Domain.BusinessRules;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class SaleCalculationTests
{
    [Fact]
    public void CalculateLineTotal_WithTax_ShouldBeCorrect()
    {
        decimal unitPrice = 10.000m;
        decimal quantity = 2;
        decimal taxRate = 16.000m;
        decimal discount = 0m;

        decimal lineSubtotal = MoneyPolicy.RoundToJOD(unitPrice * quantity - discount);
        decimal lineTax = MoneyPolicy.RoundToJOD(lineSubtotal * taxRate / 100);
        decimal lineTotal = MoneyPolicy.RoundToJOD(lineSubtotal + lineTax);

        lineSubtotal.Should().Be(20.000m);
        lineTax.Should().Be(3.200m);
        lineTotal.Should().Be(23.200m);
    }

    [Fact]
    public void CalculateTotal_WithMultipleItems_ShouldBeCorrect()
    {
        var line1Subtotal = MoneyPolicy.RoundToJOD(10.000m * 2);
        var line1Tax = MoneyPolicy.RoundToJOD(line1Subtotal * 16.000m / 100);

        var line2Subtotal = MoneyPolicy.RoundToJOD(5.500m * 3);
        var line2Tax = MoneyPolicy.RoundToJOD(line2Subtotal * 16.000m / 100);

        var subtotal = MoneyPolicy.RoundToJOD(line1Subtotal + line2Subtotal);
        var totalTax = MoneyPolicy.RoundToJOD(line1Tax + line2Tax);
        var total = MoneyPolicy.RoundToJOD(subtotal + totalTax);

        subtotal.Should().Be(36.500m);
        totalTax.Should().Be(5.840m);
        total.Should().Be(42.340m);
    }

    [Fact]
    public void CalculateChange_ShouldBeCorrect()
    {
        decimal totalDue = 23.200m;
        decimal amountPaid = 30.000m;
        decimal change = MoneyPolicy.RoundToJOD(amountPaid - totalDue);
        change.Should().Be(6.800m);
    }

    [Fact]
    public void CalculateWithDiscount_ShouldBeCorrect()
    {
        decimal subtotal = 100.000m;
        decimal discountRate = 0.10m; // 10%
        decimal discountAmount = MoneyPolicy.RoundToJOD(subtotal * discountRate);
        decimal afterDiscount = MoneyPolicy.RoundToJOD(subtotal - discountAmount);
        decimal tax = MoneyPolicy.RoundToJOD(afterDiscount * 16.000m / 100);
        decimal total = MoneyPolicy.RoundToJOD(afterDiscount + tax);

        discountAmount.Should().Be(10.000m);
        afterDiscount.Should().Be(90.000m);
        tax.Should().Be(14.400m);
        total.Should().Be(104.400m);
    }
}