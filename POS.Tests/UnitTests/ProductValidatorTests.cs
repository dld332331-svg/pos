using Xunit;
using POS.Application.Validators;
using POS.Application.DTOs;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class ProductValidatorTests
{
    [Fact]
    public void ValidateCreate_ValidRequest_ShouldReturnNoErrors()
    {
        var request = new CreateProductRequest("منتج اختبار", "Test Product", "SKU001", "1234567890", null, "Standard", "قطعة", 5.000m, 10.000m, 16.000m, 10, null, false);
        var errors = ProductValidator.ValidateCreate(request);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateCreate_EmptyArabicName_ShouldReturnError()
    {
        var request = new CreateProductRequest("", null, null, null, null, "Standard", null, 0, 10, 0, 0, null, false);
        var errors = ProductValidator.ValidateCreate(request);
        errors.Should().Contain("اسم المنتج بالعربية مطلوب");
    }

    [Fact]
    public void ValidateCreate_NegativePrice_ShouldReturnError()
    {
        var request = new CreateProductRequest("منتج", null, null, null, null, "Standard", null, 0, -1, 0, 0, null, false);
        var errors = ProductValidator.ValidateCreate(request);
        errors.Should().Contain("سعر البيع يجب أن يكون 0 أو أكبر");
    }

    [Fact]
    public void ValidateCreate_TaxRateOver100_ShouldReturnError()
    {
        var request = new CreateProductRequest("منتج", null, null, null, null, "Standard", null, 0, 10, 101, 0, null, false);
        var errors = ProductValidator.ValidateCreate(request);
        errors.Should().Contain("نسبة الضريبة يجب أن تكون بين 0 و 100");
    }

    [Fact]
    public void ValidateCreate_LongName_ShouldReturnError()
    {
        var request = new CreateProductRequest("Test", new string('أ', 201), null, null, null, "Standard", null, 0, 10, 0, 0, null, false);
        var errors = ProductValidator.ValidateCreate(request);
        errors.Should().Contain("اسم المنتج يجب ألا يتجاوز 200 حرف");
    }
}