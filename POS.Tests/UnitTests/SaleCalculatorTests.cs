using Xunit;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.BusinessRules;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class SaleCalculatorTests
{
    // ========================================================================
    // CreateItem(ProductDto) — overload with product + quantity + discount
    // ========================================================================

    [Fact]
    public void CreateItem_FromProduct_WithNoDiscount_ShouldComputeCorrectly()
    {
        var product = new ProductDto(
            Guid.NewGuid(), "Latte", "لاتيه", "SKU001", "123456",
            null, "مشروبات", "Item", "cup", 2.000m, 10.000m, 0.16m, 5.000m,
            null, null, "Active", false, 100);

        var result = SaleCalculator.CreateItem(null, product, 2);

        result.ProductId.Should().Be(product.Id);
        result.ProductName.Should().Be("لاتيه");
        result.Quantity.Should().Be(2);
        result.UnitPrice.Should().Be(10.000m);
        result.Discount.Should().Be(0);
        result.TaxRate.Should().Be(0.16m);

        // lineBeforeTax = 10 * 2 - 0 = 20.000
        // taxAmount = 20.000 * 0.16 = 3.200
        // lineTotal = 20.000 + 3.200 = 23.200
        result.TaxAmount.Should().Be(3.200m);
        result.LineTotal.Should().Be(23.200m);
        result.Cost.Should().Be(2.000m);
    }

    [Fact]
    public void CreateItem_FromProduct_WithDiscount_ShouldSubtractDiscount()
    {
        var product = new ProductDto(
            Guid.NewGuid(), "Muffin", "مفن", null, null,
            null, "مخبوزات", "Item", "piece", 1.500m, 5.000m, 0.10m, 10.000m,
            null, null, "Active", false, 50);

        var result = SaleCalculator.CreateItem(null, product, 3, 2.000m);

        // lineBeforeTax = 5 * 3 - 2.000 = 13.000
        // lineBeforeTax = 5 * 3 - 2.000 = 13.000
        // taxAmount = 13.000 * 0.10 = 1.300
        // lineTotal = 13.000 + 1.300 = 14.300
        result.TaxAmount.Should().Be(1.300m);
        result.LineTotal.Should().Be(14.300m);
        result.Discount.Should().Be(2.000m);
    }

    [Fact]
    public void CreateItem_FromProduct_WithZeroQuantity_ShouldComputeZero()
    {
        var product = new ProductDto(
            Guid.NewGuid(), "Water", "ماء", null, null,
            null, "مشروبات", "Item", "bottle", 0.250m, 1.000m, 0m, 20.000m,
            null, null, "Active", false, 200);

        var result = SaleCalculator.CreateItem(null, product, 0);

        result.Quantity.Should().Be(0);
        result.UnitPrice.Should().Be(1.000m);
        result.TaxAmount.Should().Be(0m);
        result.LineTotal.Should().Be(0m);
    }

    [Fact]
    public void CreateItem_FromProduct_NullProduct_ShouldThrow()
    {
        var act = () => SaleCalculator.CreateItem(null, null!, 1);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // CreateItem(explicit params) — overload with explicit field values
    // ========================================================================

    [Fact]
    public void CreateItem_FromExplicitParams_ShouldComputeCorrectly()
    {
        var result = SaleCalculator.CreateItem(
            null, Guid.NewGuid(), "Espresso", 8.000m,
            2, 0.500m, 0.16m, 1.200m, null, null);

        // lineBeforeTax = 8 * 2 - 0.500 = 15.500
        // taxAmount = 15.500 * 0.16 = 2.480
        // lineTotal = 15.500 + 2.480 = 17.980
        result.Quantity.Should().Be(2);
        result.UnitPrice.Should().Be(8.000m);
        result.Discount.Should().Be(0.500m);
        result.TaxAmount.Should().Be(2.480m);
        result.LineTotal.Should().Be(17.980m);
        result.Cost.Should().Be(1.200m);
        result.Notes.Should().BeNull();
        result.ModifierSummary.Should().BeNull();
    }

    [Fact]
    public void CreateItem_FromExplicitParams_WithCustomId_ShouldPreserveId()
    {
        var id = Guid.NewGuid();
        var result = SaleCalculator.CreateItem(
            id, Guid.NewGuid(), "Tea", 3.000m,
            1, 0, 0m, 0.500m, "Some notes", "Sugar");

        result.Id.Should().Be(id);
        result.Notes.Should().Be("Some notes");
        result.ModifierSummary.Should().Be("Sugar");
    }

    [Fact]
    public void CreateItem_FromExplicitParams_ZeroTax_ShouldHaveNoTaxAmount()
    {
        var result = SaleCalculator.CreateItem(
            null, Guid.NewGuid(), "Item", 10.000m,
            1, 0, 0m, 5.000m, null, null);

        result.TaxAmount.Should().Be(0m);
        result.LineTotal.Should().Be(10.000m);
    }

    [Fact]
    public void CreateItem_FromExplicitParams_NegativeDiscount_ShouldHandleCorrectly()
    {
        var result = SaleCalculator.CreateItem(
            null, Guid.NewGuid(), "Item", 10.000m,
            1, -2.000m, 0.16m, 5.000m, null, null);

        // lineBeforeTax = 10 * 1 - (-2) = 12.000
        // taxAmount = 12.000 * 0.16 = 1.920
        // lineTotal = 12.000 + 1.920 = 13.920
        result.TaxAmount.Should().Be(1.920m);
        result.LineTotal.Should().Be(13.920m);
    }

    // ========================================================================
    // RecalculateItem — recompute a single item's tax/total
    // ========================================================================

    [Fact]
    public void RecalculateItem_WithoutModifiers_ShouldRecomputeCorrectly()
    {
        var item = new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Coffee", 3, 10.000m,
            1.000m, 0.16m, 0, 0, 3.000m, null, null);

        var result = SaleCalculator.RecalculateItem(item);

        // lineBeforeTax = 10 * 3 - 1 = 29.000
        // taxAmount = 29.000 * 0.16 = 4.640
        // lineTotal = 29.000 + 4.640 = 33.640
        result.TaxAmount.Should().Be(4.640m);
        result.LineTotal.Should().Be(33.640m);
        result.Quantity.Should().Be(3); // Other fields preserved
        result.UnitPrice.Should().Be(10.000m);
    }

    [Fact]
    public void RecalculateItem_WithModifierExtra_ShouldIncludeModifierInTotal()
    {
        var item = new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Sandwich", 1, 15.000m,
            0, 0.10m, 0, 0, 8.000m, null, null);

        var result = SaleCalculator.RecalculateItem(item, modifierExtra: 3.500m);

        // lineBeforeTax = 15 * 1 + 3.500 - 0 = 18.500
        // taxAmount = 18.500 * 0.10 = 1.850
        // lineTotal = 18.500 + 1.850 = 20.350
        result.TaxAmount.Should().Be(1.850m);
        result.LineTotal.Should().Be(20.350m);
    }

    [Fact]
    public void RecalculateItem_NullItem_ShouldThrow()
    {
        var act = () => SaleCalculator.RecalculateItem(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecalculateItem_ShouldPreserveImmutableFields()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var item = new SaleItemDto(
            id, productId, "Juice", 2, 7.000m,
            0, 0.16m, 0, 0, 3.500m, "No ice", null);

        var result = SaleCalculator.RecalculateItem(item);

        result.Id.Should().Be(id);
        result.ProductId.Should().Be(productId);
        result.ProductName.Should().Be("Juice");
        result.Notes.Should().Be("No ice");
        result.Cost.Should().Be(3.500m);
    }

    // ========================================================================
    // CalculateTotals — compute aggregate totals from item list
    // ========================================================================

    [Fact]
    public void CalculateTotals_SingleItem_ShouldMatchLineTotals()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 2, 0.16m, 0, 23.200m, 3.200m)
        };

        var totals = SaleCalculator.CalculateTotals(items);

        // subtotal = 10 * 2 = 20.000
        // tax = 3.200
        // discount = 0
        // total = 20.000 + 3.200 - 0 = 23.200
        totals.SubTotal.Should().Be(20.000m);
        totals.Tax.Should().Be(3.200m);
        totals.Discount.Should().Be(0m);
        totals.Total.Should().Be(23.200m);
    }

    [Fact]
    public void CalculateTotals_MultipleItems_ShouldSumCorrectly()
    {
        var items = new List<SaleItemDto>
        {
            // LineTotal = 23.200, tax = 3.200, discount = 0
            CreateSampleItem(10.000m, 2, 0.16m, 0, 23.200m, 3.200m),
            // LineTotal = 14.300, tax = 1.300, discount = 2.000
            CreateSampleItem(5.000m, 3, 0.10m, 2.000m, 14.300m, 1.300m)
        };

        var totals = SaleCalculator.CalculateTotals(items);

        // subtotal = 10*2 + 5*3 = 35.000
        // tax = 3.200 + 1.300 = 4.500
        // discount = 0 + 2.000 = 2.000
        // total = 35.000 + 4.500 - 2.000 = 37.500
        totals.SubTotal.Should().Be(35.000m);
        totals.Tax.Should().Be(4.500m);
        totals.Discount.Should().Be(2.000m);
        totals.Total.Should().Be(37.500m);
    }

    [Fact]
    public void CalculateTotals_EmptyList_ShouldReturnZeros()
    {
        var totals = SaleCalculator.CalculateTotals(new List<SaleItemDto>());

        totals.SubTotal.Should().Be(0m);
        totals.Tax.Should().Be(0m);
        totals.Discount.Should().Be(0m);
        totals.Total.Should().Be(0m);
    }

    [Fact]
    public void CalculateTotals_NullItems_ShouldThrow()
    {
        var act = () => SaleCalculator.CalculateTotals(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateTotals_Rounding_ShouldNotProduceUnnecessaryDecimals()
    {
        // Items with 3-decimal unit prices to test rounding
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.123m, 3, 0.10m, 1.000m, 30.306m, 3.027m)
        };

        var totals = SaleCalculator.CalculateTotals(items);

        totals.SubTotal.Should().Be(30.369m); // 10.123 * 3 = 30.369
        totals.Tax.Should().Be(3.027m);
        totals.Discount.Should().Be(1.000m);
        totals.Total.Should().Be(32.396m); // 30.369 + 3.027 - 1.000 = 32.396
        decimal.Round(totals.Total, 3).Should().Be(totals.Total); // Exactly 3 decimals
    }

    // ========================================================================
    // GetTotal — Sum(LineTotal) - Sum(Discount)
    // ========================================================================

    [Fact]
    public void GetTotal_SingleItem_ShouldReturnLineTotal()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 2, 0.16m, 0, 23.200m, 3.200m)
        };

        var total = SaleCalculator.GetTotal(items);
        total.Should().Be(23.200m);
    }

    [Fact]
    public void GetTotal_MultipleItems_ShouldSumLineTotalsMinusDiscount()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 2, 0.16m, 0, 23.200m, 3.200m),
            CreateSampleItem(5.000m, 3, 0.10m, 2.000m, 14.300m, 1.300m)
        };

        // 23.200 + 14.300 - (0 + 2.000) = 35.500
        var total = SaleCalculator.GetTotal(items);
        total.Should().Be(35.500m);
    }

    [Fact]
    public void GetTotal_WithDiscounts_ShouldSubtractFromLineTotals()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0.10m, 5.000m, 5.500m, 0.500m)
        };

        // LineTotal = 5.500, Discount = 5.000 → total = 0.500
        var total = SaleCalculator.GetTotal(items);
        total.Should().Be(0.500m);
    }

    [Fact]
    public void GetTotal_NullItems_ShouldThrow()
    {
        var act = () => SaleCalculator.GetTotal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // GetChange
    // ========================================================================

    [Fact]
    public void GetChange_ExactAmount_ShouldReturnZero()
    {
        var change = SaleCalculator.GetChange(23.200m, 23.200m);
        change.Should().Be(0m);
    }

    [Fact]
    public void GetChange_Overpayment_ShouldReturnPositive()
    {
        var change = SaleCalculator.GetChange(30.000m, 23.200m);
        change.Should().Be(6.800m);
    }

    [Fact]
    public void GetChange_Underpayment_ShouldReturnNegative()
    {
        var change = SaleCalculator.GetChange(20.000m, 23.200m);
        change.Should().Be(-3.200m);
    }

    [Fact]
    public void GetChange_ZeroPayment_ShouldReturnNegativeFullAmount()
    {
        var change = SaleCalculator.GetChange(0m, 23.200m);
        change.Should().Be(-23.200m);
    }

    // ========================================================================
    // DistributeDiscount — proportional discount distribution
    // ========================================================================

    [Fact]
    public void DistributeDiscount_TwoItems_ShouldDistributeProportionally()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m), // 40% of total
            CreateSampleItem(15.000m, 1, 0m, 0, 15.000m, 0m)  // 60% of total
        };

        var result = SaleCalculator.DistributeDiscount(items, 5.000m);

        // Total = 10 + 15 = 25
        // Item 0 proportion = 10/25 = 40% → discount = 5 * 0.4 = 2.000
        // Item 1 proportion = 15/25 = 60% → discount = 5 * 0.6 = 3.000
        result[0].Discount.Should().Be(2.000m);
        result[1].Discount.Should().Be(3.000m);
    }

    [Fact]
    public void DistributeDiscount_ZeroDiscount_ShouldReturnUnchanged()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 0);
        result[0].Discount.Should().Be(0m);
    }

    [Fact]
    public void DistributeDiscount_EmptyList_ShouldReturnEmpty()
    {
        var result = SaleCalculator.DistributeDiscount(new List<SaleItemDto>(), 5.000m);
        result.Should().BeEmpty();
    }

    [Fact]
    public void DistributeDiscount_SingleItem_ShouldGetAllDiscount()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 5.000m);
        result[0].Discount.Should().Be(5.000m);
    }

    [Fact]
    public void DistributeDiscount_ShouldPreserveOriginalItems_ReturnsNewList()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 3.000m);

        // Original should be unchanged
        items[0].Discount.Should().Be(0m);
        // Result should have the discount
        result[0].Discount.Should().Be(3.000m);
        // Should be different references
        result.Should().NotBeSameAs(items);
    }

    [Fact]
    public void DistributeDiscount_WithExistingDiscounts_ShouldAccumulate()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 1.000m, 10.000m, 0m), // Already has 1 discount
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 5.000m);

        // GetTotal = 10 + 10 - 1 = 19 (discount reduces denominator)
        // Item 0 proportion = 10/19 → discount = 5 * 10/19 = 2.632
        // Item 1 proportion = 10/19 → discount = 5 * 10/19 = 2.632
        // Sum distributed = 5.264, rounding diff = -0.264 → last item adjusted
        // Item 1 adjusted: 2.632 - 0.264 = 2.368
        // Result[0].Discount = existing 1.000 + new 2.632 = 3.632
        // Result[1].Discount = 0 + new 2.368 = 2.368
        // Total of all discounts (including existing) = 3.632 + 2.368 = 6.000
        var newlyDistributed = result.Sum(i => i.Discount - items[result.IndexOf(i)].Discount);
        newlyDistributed.Should().Be(5.000m); // Newly distributed discount matches
        result[0].Discount.Should().Be(3.632m);
        result[1].Discount.Should().Be(2.368m);
    }

    [Fact]
    public void DistributeDiscount_ThreeItems_RoundingShouldntLoseMoney()
    {
        // Test rounding compensation: 5.000 discount across 3 items
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m),
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m),
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 5.000m);

        // Each gets 5/3 = 1.667, but rounding means sum should still be 5.000
        var totalDiscountApplied = result.Sum(i => i.Discount);
        totalDiscountApplied.Should().Be(5.000m);
    }

    [Fact]
    public void DistributeDiscount_NegativeDiscountAmount_ShouldReturnUnchanged()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0m, 0, 10.000m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, -5.000m);
        result[0].Discount.Should().Be(0m);
    }

    [Fact]
    public void DistributeDiscount_NullItems_ShouldThrow()
    {
        var act = () => SaleCalculator.DistributeDiscount(null!, 5.000m);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DistributeDiscount_AllZeroLineTotals_ShouldReturnUnchanged()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(0m, 1, 0m, 0, 0m, 0m)
        };

        var result = SaleCalculator.DistributeDiscount(items, 5.000m);
        result[0].Discount.Should().Be(0m);
    }

    // ========================================================================
    // Edge Cases — Large Values, High Precision
    // ========================================================================

    [Fact]
    public void CalculateTotals_LargeValues_ShouldNotOverflow()
    {
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(99999.999m, 9999, 10.000m, 0, 1_099_989_989.010m, 99_998_999.010m)
        };

        var totals = SaleCalculator.CalculateTotals(items);
        totals.SubTotal.Should().BeGreaterThan(0);
        totals.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetChange_Rounding_ShouldReturnPreciseThreeDecimals()
    {
        var change = SaleCalculator.GetChange(100.000m, 33.333m);
        change.Should().Be(66.667m);
        decimal.Round(change, 3).Should().Be(change);
    }

    [Fact]
    public void RecalculateItem_LargeQuantity_ShouldHandleCorrectly()
    {
        var item = new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Bulk Item", 1000, 0.100m,
            0, 0.05m, 0, 0, 0.050m, null, null);

        var result = SaleCalculator.RecalculateItem(item);

        // lineBeforeTax = 0.1 * 1000 - 0 = 100.000
        // taxAmount = 100.000 * 0.05 = 5.000
        // lineTotal = 100.000 + 5.000 = 105.000
        result.TaxAmount.Should().Be(5.000m);
        result.LineTotal.Should().Be(105.000m);
    }

    [Fact]
    public void CalculateTotals_RoundToJOD_EnsuresExactlyThreeDecimals()
    {
        // Test that rounding produces exactly 3 decimal places
        var items = new List<SaleItemDto>
        {
            CreateSampleItem(10.000m, 1, 0.16m, 0, 11.600m, 1.600m)
        };

        var totals = SaleCalculator.CalculateTotals(items);
        var totalStr = totals.Total.ToString("0.000");
        totalStr.Should().MatchRegex(@"^\d+\.\d{3}$");
    }

    // ========================================================================
    // Helper: create a sample SaleItemDto for tests
    // ========================================================================

    private static SaleItemDto CreateSampleItem(
        decimal unitPrice, decimal quantity, decimal taxRate,
        decimal discount, decimal lineTotal, decimal taxAmount)
    {
        return new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Test Product", quantity,
            unitPrice, discount, taxRate, taxAmount, lineTotal,
            0m, null, null);
    }
}
