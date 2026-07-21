using Xunit;
using POS.Application.Validators;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class SaleValidatorTests
{
    [Fact]
    public void ValidatePayment_ValidAmount_ShouldReturnNoErrors()
    {
        var errors = SaleValidator.ValidatePayment(10.000m, 10.000m);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePayment_ZeroPaid_ShouldReturnError()
    {
        var errors = SaleValidator.ValidatePayment(10.000m, 0m);
        errors.Should().Contain("المبلغ المدفوع يجب أن يكون أكبر من صفر");
    }

    [Fact]
    public void ValidatePayment_InsufficientAmount_ShouldReturnError()
    {
        var errors = SaleValidator.ValidatePayment(10.000m, 5.000m);
        errors.Should().Contain("المبلغ المدفوع أقل من المبلغ المطلوب");
    }

    [Fact]
    public void ValidateDiscount_ValidDiscount_ShouldReturnNoErrors()
    {
        var errors = SaleValidator.ValidateDiscount(2.000m, 10.000m);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiscount_ExceedsSubtotal_ShouldReturnError()
    {
        var errors = SaleValidator.ValidateDiscount(15.000m, 10.000m);
        errors.Should().Contain("مبلغ الخصم يتجاوز المبلغ الإجمالي");
    }
}