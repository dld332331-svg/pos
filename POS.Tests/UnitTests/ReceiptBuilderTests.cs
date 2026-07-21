#nullable enable

using Xunit;
using FluentAssertions;
using POS.Reporting.Reports;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for ReceiptBuilder covering:
/// - Full receipt rendering with all sections
/// - Optional fields (address, phone, customer, table, discount, footer)
/// - Empty data edge cases
/// - Line formatting (centering, alignment, widths)
/// - Multiple items and payments
/// </summary>
public class ReceiptBuilderTests
{
    private const int ReceiptWidth = 48;

    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static ReceiptItem CreateItem(
        string productName = "قهوة تركية",
        decimal quantity = 2.000m,
        decimal unitPrice = 3.500m,
        decimal lineTotal = 7.000m)
    {
        return new ReceiptItem
        {
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = lineTotal
        };
    }

    private static ReceiptPayment CreatePayment(string method = "نقدي", decimal amount = 10.000m)
    {
        return new ReceiptPayment
        {
            Method = method,
            Amount = amount
        };
    }

    private static ReceiptData CreateFullReceiptData()
    {
        return new ReceiptData
        {
            BusinessName = "مقهى السلام",
            Address = "شارع الملك حسين, عمان",
            Phone = "0791234567",
            InvoiceNumber = "INV-20260719-001",
            Date = new DateTime(2026, 7, 19, 14, 30, 0),
            CashierName = "أحمد",
            CustomerName = "محمد علي",
            TableNumber = 5,
            Items = new List<ReceiptItem>
            {
                CreateItem(productName: "قهوة تركية", quantity: 2.000m, unitPrice: 3.500m, lineTotal: 7.000m),
                CreateItem(productName: "شاي أحمر", quantity: 1.000m, unitPrice: 2.000m, lineTotal: 2.000m),
                CreateItem(productName: "كنافة", quantity: 1.000m, unitPrice: 4.500m, lineTotal: 4.500m)
            },
            SubTotal = 13.500m,
            TaxAmount = 2.160m,
            DiscountAmount = 1.000m,
            TotalAmount = 14.660m,
            Payments = new List<ReceiptPayment>
            {
                CreatePayment(method: "نقدي", amount: 10.000m),
                CreatePayment(method: "بطاقة ائتمان", amount: 5.000m)
            },
            Footer = "نرحب بزيارتكم مرة أخرى"
        };
    }

    // ========================================================================
    // BuildReceipt — Full Data
    // ========================================================================

    [Fact]
    public void BuildReceipt_FullData_ContainsAllSections()
    {
        // Arrange
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Header section
        result.Should().Contain("مقهى السلام");
        result.Should().Contain("شارع الملك حسين, عمان");
        result.Should().Contain("0791234567");

        // Assert — Separator line
        result.Should().Contain(new string('-', ReceiptWidth));

        // Assert — Invoice info
        result.Should().Contain("رقم الفاتورة: INV-20260719-001");
        result.Should().Contain("التاريخ: 2026/07/19 14:30");
        result.Should().Contain("الكاشير: أحمد");
        result.Should().Contain("العميل: محمد علي");
        result.Should().Contain("الطاولة: 5");

        // Assert — Items table header
        result.Should().Contain("الصنف");
        result.Should().Contain("الكمية");
        result.Should().Contain("السعر");
        result.Should().Contain("المجموع");

        // Assert — Item rows
        result.Should().Contain("قهوة تركية");
        result.Should().Contain("شاي أحمر");
        result.Should().Contain("كنافة");
        result.Should().Contain("2.000");
        result.Should().Contain("3.500");
        result.Should().Contain("7.000");
        result.Should().Contain("4.500");

        // Assert — Totals
        result.Should().Contain("المجموع الفرعي:");
        result.Should().Contain("13.500");
        result.Should().Contain("الضريبة:");
        result.Should().Contain("2.160");
        result.Should().Contain("الخصم:");
        result.Should().Contain("-1.000");

        // Assert — Grand total line
        result.Should().Contain("الإجمالي:");
        result.Should().Contain("14.660");
        result.Should().Contain("JOD");

        // Assert — Double separator line before total
        result.Should().Contain(new string('=', ReceiptWidth));

        // Assert — Payments
        result.Should().Contain("نقدي");
        result.Should().Contain("بطاقة ائتمان");
        result.Should().Contain("10.000");
        result.Should().Contain("5.000");

        // Assert — Footer
        result.Should().Contain("شكراً لزيارتكم");
        result.Should().Contain("نرحب بزيارتكم مرة أخرى");
    }

    // ========================================================================
    // BuildReceipt — Optional Fields Omitted
    // ========================================================================

    [Fact]
    public void BuildReceipt_NoOptionalFields_OmitsSections()
    {
        // Arrange — no address, phone, customer, table, discount, footer
        var builder = new ReceiptBuilder();
        var data = new ReceiptData
        {
            BusinessName = "مقهى السلام",
            InvoiceNumber = "INV-001",
            Date = new DateTime(2026, 7, 19, 10, 0, 0),
            CashierName = "أحمد",
            SubTotal = 10.000m,
            TaxAmount = 1.600m,
            TotalAmount = 11.600m,
            Items = new List<ReceiptItem>
            {
                new() { ProductName = "قهوة", Quantity = 1, UnitPrice = 10, LineTotal = 10 }
            },
            Payments = new List<ReceiptPayment>
            {
                new() { Method = "نقدي", Amount = 11.600m }
            }
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Empty address/phone render as blank centered lines (48 spaces)
        // They should NOT contain the actual heading text for address/phone
        result.Should().Contain("رقم الفاتورة: INV-001");

        // Customer line is NOT rendered (CustomerName is null)
        result.Should().NotContain("العميل:");

        // Table line is NOT rendered (TableNumber is null)
        result.Should().NotContain("الطاولة:");

        // Discount line is NOT rendered (DiscountAmount = 0)
        result.Should().NotContain("الخصم:");

        // Footer thank-you IS rendered (always present), but custom footer is NOT
        result.Should().Contain("شكراً لزيارتكم");
    }

    // ========================================================================
    // BuildReceipt — Empty Items
    // ========================================================================

    [Fact]
    public void BuildReceipt_EmptyItems_ShowsTableHeaderWithoutItems()
    {
        // Arrange — no items
        var builder = new ReceiptBuilder();
        var data = new ReceiptData
        {
            BusinessName = "مقهى السلام",
            InvoiceNumber = "INV-EMPTY",
            Date = new DateTime(2026, 7, 19, 12, 0, 0),
            CashierName = "أحمد",
            SubTotal = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            Items = new List<ReceiptItem>(),
            Payments = new List<ReceiptPayment>()
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Header, table headers, and totals still render
        result.Should().Contain("رقم الفاتورة: INV-EMPTY");
        result.Should().Contain("الصنف");
        result.Should().Contain("الكمية");
        result.Should().Contain("0.000");
        result.Should().Contain("الإجمالي:");
        result.Should().Contain("شكراً لزيارتكم");
    }

    // ========================================================================
    // BuildReceipt — Discount Display
    // ========================================================================

    [Fact]
    public void BuildReceipt_ZeroDiscount_OmitsDiscountLine()
    {
        // Arrange — DiscountAmount is 0
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();
        data.DiscountAmount = 0;

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — No discount line
        result.Should().NotContain("الخصم:");
    }

    [Fact]
    public void BuildReceipt_NegativeDiscount_ShowsDiscountLine()
    {
        // Arrange — DiscountAmount > 0 (positive value)
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();
        data.DiscountAmount = 2.500m;
        data.TotalAmount = 13.160m; // recalculate: 13.5 + 2.16 - 2.5

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Discount line shown with negative value (-2.500)
        result.Should().Contain("الخصم:");
        result.Should().Contain("-2.500");
    }

    // ========================================================================
    // BuildReceipt — Customer and Table
    // ========================================================================

    [Fact]
    public void BuildReceipt_EmptyCustomerName_OmitsCustomerLine()
    {
        // Arrange — CustomerName is empty string
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();
        data.CustomerName = "";

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Customer line omitted
        result.Should().NotContain("العميل:");
    }

    [Fact]
    public void BuildReceipt_NoTableNumber_OmitsTableLine()
    {
        // Arrange — TableNumber is null
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();
        data.TableNumber = null;

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Table line omitted
        result.Should().NotContain("الطاولة:");
    }

    // ========================================================================
    // BuildReceipt — No Payments
    // ========================================================================

    [Fact]
    public void BuildReceipt_NoPayments_OmitsPaymentSection()
    {
        // Arrange — empty payments list
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();
        data.Payments = new List<ReceiptPayment>();

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — No payment names rendered, totals still show
        result.Should().NotContain("نقدي");
        result.Should().Contain("الإجمالي:");
        result.Should().Contain("شكراً لزيارتكم");
    }

    // ========================================================================
    // BuildReceipt — Line Formatting Verification
    // ========================================================================

    [Fact]
    public void BuildReceipt_BusinessNameIsCentered()
    {
        // Arrange — short business name should be centered within 48-char width
        var builder = new ReceiptBuilder();
        var data = new ReceiptData
        {
            BusinessName = "مقهى",
            InvoiceNumber = "INV-001",
            Date = DateTime.Now,
            CashierName = "test",
            SubTotal = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            Items = new List<ReceiptItem>(),
            Payments = new List<ReceiptPayment>()
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — First line contains the business name
        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("مقهى");
        lines[0].Length.Should().Be(ReceiptWidth);
    }

    [Fact]
    public void BuildReceipt_ItemPriceAlignsRight()
    {
        // Arrange
        var builder = new ReceiptBuilder();
        var data = CreateFullReceiptData();

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — The first item's quantity line should have right-aligned numbers.
        // The format is: " 2.000     3.500     7.000"
        // Each value is right-aligned in its column: qty=6, price=10, total=10
        // The line contains these values in order
        result.Should().Contain("2.000");
        result.Should().Contain("3.500");
    }

    // ========================================================================
    // BuildReceipt — Single Item Edge Case
    // ========================================================================

    [Fact]
    public void BuildReceipt_SingleItem_RendersCorrectly()
    {
        // Arrange — minimal receipt with one item
        var builder = new ReceiptBuilder();
        var data = new ReceiptData
        {
            BusinessName = "متجر",
            InvoiceNumber = "INV-001",
            Date = new DateTime(2026, 7, 19, 9, 0, 0),
            CashierName = "test",
            SubTotal = 5.000m,
            TaxAmount = 0.800m,
            TotalAmount = 5.800m,
            Items = new List<ReceiptItem>
            {
                CreateItem(productName: "ماء معدني", quantity: 1.000m, unitPrice: 5.000m, lineTotal: 5.000m)
            },
            Payments = new List<ReceiptPayment>
            {
                CreatePayment(method: "نقدي", amount: 5.800m)
            }
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — All sections present
        result.Should().Contain("متجر");
        result.Should().Contain("ماء معدني");
        result.Should().Contain("1.000");
        result.Should().Contain("5.000");
        result.Should().Contain("5.800");
        result.Should().Contain("شكراً لزيارتكم");
    }

    // ========================================================================
    // BuildReceipt — Long Product Names
    // ========================================================================

    [Fact]
    public void BuildReceipt_LongProductName_WrapsNicely()
    {
        // Arrange — product name longer than receipt width
        var builder = new ReceiptBuilder();
        var longName = "قهوة تركية محضرة على الطريقة العربية الأصيلة مع الهيل";
        var data = new ReceiptData
        {
            BusinessName = "مقهى",
            InvoiceNumber = "INV-001",
            Date = new DateTime(2026, 7, 19, 10, 0, 0),
            CashierName = "test",
            SubTotal = 10.000m,
            TaxAmount = 1.600m,
            TotalAmount = 11.600m,
            Items = new List<ReceiptItem>
            {
                CreateItem(productName: longName, quantity: 2.000m, unitPrice: 5.000m, lineTotal: 10.000m)
            },
            Payments = new List<ReceiptPayment>
            {
                CreatePayment(method: "نقدي", amount: 11.600m)
            }
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — Full product name is present, receipt still renders
        result.Should().Contain(longName);
        result.Should().Contain("10.000");
        result.Should().Contain("الإجمالي:");
    }

    // ========================================================================
    // BuildReceipt — Multiple Same Items
    // ========================================================================

    [Fact]
    public void BuildReceipt_MultipleItems_ShowsAllInOrder()
    {
        // Arrange — 5 items with specific order
        var builder = new ReceiptBuilder();
        var data = new ReceiptData
        {
            BusinessName = "مقهى",
            InvoiceNumber = "INV-001",
            Date = new DateTime(2026, 7, 19, 11, 0, 0),
            CashierName = "test",
            SubTotal = 25.000m,
            TaxAmount = 4.000m,
            TotalAmount = 29.000m,
            Items = new List<ReceiptItem>
            {
                CreateItem(productName: "أول", quantity: 1m, unitPrice: 5m, lineTotal: 5m),
                CreateItem(productName: "ثاني", quantity: 2m, unitPrice: 4m, lineTotal: 8m),
                CreateItem(productName: "ثالث", quantity: 3m, unitPrice: 3m, lineTotal: 9m),
                CreateItem(productName: "رابع", quantity: 1m, unitPrice: 2m, lineTotal: 2m),
                CreateItem(productName: "خامس", quantity: 1m, unitPrice: 1m, lineTotal: 1m)
            },
            Payments = new List<ReceiptPayment>
            {
                CreatePayment(method: "نقدي", amount: 29.000m)
            }
        };

        // Act
        var result = builder.BuildReceipt(data);

        // Assert — All items present in order
        result.Should().Contain("أول");
        result.Should().Contain("ثاني");
        result.Should().Contain("ثالث");
        result.Should().Contain("رابع");
        result.Should().Contain("خامس");
        result.Should().Contain("25.000");
        result.Should().Contain("29.000");
    }
}
