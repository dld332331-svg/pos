#nullable enable

using Xunit;
using FluentAssertions;
using POS.Reporting.Reports;
using POS.Application.DTOs;

namespace POS.Tests.UnitTests;

public class ReportBuilderTests
{
    private static readonly DateTime TestDate = new(2026, 7, 19);
    private const string BusinessName = "\u0645\u062A\u062C\u0631 \u0627\u0644\u0627\u062E\u062A\u0628\u0627\u0631";

    // ========================================================================
    // SaleReportBuilder — Daily Sales Report
    // ========================================================================

    private static DailySalesReportData CreateDailySalesData()
    {
        return new DailySalesReportData(
            HourlySales: new List<HourlySalesDto>
            {
                new(10, 150.000m, 5, 30.000m),
                new(11, 230.000m, 8, 28.750m),
                new(12, 310.500m, 12, 25.875m),
                new(13, 175.250m, 6, 29.208m)
            },
            PaymentBreakdown: new List<PaymentMethodSalesDto>
            {
                new("\u0646\u0642\u062F\u064A", 520.500m, 12, 48.14m),
                new("\u0628\u0637\u0627\u0642\u0629", 345.250m, 19, 31.86m)
            },
            TopProducts: new List<ProductSalesRankDto>
            {
                new("Coffee Latte", 45.000m, 540.000m, 30),
                new("Croissant", 32.000m, 256.000m, 28),
                new("Sandwich", 18.000m, 270.000m, 15)
            },
            Refunds: new List<RefundSummaryDto>
            {
                new("REF-001", 12.500m, "\u0627\u0646\u062A\u0647\u0627\u0621 \u0627\u0644\u0635\u0644\u0627\u062D\u064A\u0629", new DateTime(2026, 7, 19, 14, 30, 0), "Milk"),
                new("REF-002", 8.000m, "\u062E\u0637\u0623 \u0641\u064A \u0627\u0644\u0637\u0644\u0628", new DateTime(2026, 7, 19, 15, 0, 0), "Juice")
            },
            GrandTotal: 865.750m,
            GrandTax: 119.413m,
            GrandDiscount: 45.000m,
            TotalTransactions: 31,
            NetSales: 701.337m);
    }

    [Fact]
    public void BuildDailySalesReport_WithFullData_ContainsAllSections()
    {
        var builder = new SaleReportBuilder();
        var data = CreateDailySalesData();
        var result = builder.BuildDailySalesReport(TestDate, BusinessName, data);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A \u0627\u0644\u064A\u0648\u0645\u064A\u0629");
        text.Should().Contain(BusinessName);
        text.Should().Contain("2026/07/19");
        text.Should().Contain("\u0627\u0644\u062A\u0648\u0632\u064A\u0639 \u0627\u0644\u0633\u0627\u0639\u064A");
        text.Should().Contain("10:00 - 11:00");
        text.Should().Contain("150.000");
        text.Should().Contain("310.500");
        text.Should().Contain("\u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A \u062D\u0633\u0628 \u0637\u0631\u064A\u0642\u0629 \u0627\u0644\u062F\u0641\u0639");
        text.Should().Contain("520.500");
        text.Should().Contain("345.250");
        text.Should().Contain("\u0623\u0639\u0644\u0649 \u0627\u0644\u0645\u0646\u062A\u062C\u0627\u062A \u0645\u0628\u064A\u0639\u0627\u064B");
        text.Should().Contain("Coffee Latte");
        text.Should().Contain("540.000");
        text.Should().Contain("\u0627\u0644\u0645\u0631\u062A\u062C\u0639\u0627\u062A");
        text.Should().Contain("REF-001");
        text.Should().Contain("20.500");
        text.Should().Contain("\u0627\u0644\u0645\u0644\u062E\u0635 \u0627\u0644\u0625\u062C\u0645\u0627\u0644\u064A");
        text.Should().Contain("865.750");
        text.Should().Contain("119.413");
        text.Should().Contain("701.337");
        text.Should().Contain("31");
        text.Should().Contain("\u062A\u0645 \u0625\u0639\u062F\u0627\u062F \u0627\u0644\u062A\u0642\u0631\u064A\u0631 \u0641\u064A");
    }

    [Fact]
    public void BuildDailySalesReport_WithNoData_ShowsEmptyMessages()
    {
        var builder = new SaleReportBuilder();
        var data = new DailySalesReportData(new List<HourlySalesDto>(), new List<PaymentMethodSalesDto>(),
            new List<ProductSalesRankDto>(), new List<RefundSummaryDto>(), 0, 0, 0, 0, 0);
        var result = builder.BuildDailySalesReport(TestDate, BusinessName, data);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0645\u0628\u064A\u0639\u0627\u062A \u0641\u064A \u0647\u0630\u0627 \u0627\u0644\u064A\u0648\u0645");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0637\u0631\u0642 \u062F\u0641\u0639 \u0645\u062A\u0627\u062D\u0629");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0645\u0646\u062A\u062C\u0627\u062A \u0645\u062A\u0627\u062D\u0629");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0645\u0631\u062A\u062C\u0639\u0627\u062A \u0641\u064A \u0647\u0630\u0627 \u0627\u0644\u064A\u0648\u0645");
        text.Should().Contain("0.000");
    }

    // ========================================================================
    // SaleReportBuilder — Sales By Category
    // ========================================================================

    [Fact]
    public void BuildSalesByCategoryReport_WithData_ContainsCategoryTableAndDistribution()
    {
        var builder = new SaleReportBuilder();
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 19);
        var data = new SalesByCategoryReportData(
            new List<CategorySalesDto>
            {
                new("\u0645\u0634\u0631\u0648\u0628\u0627\u062A", 120, 1800.000m, 15.000m, 80),
                new("\u0645\u062E\u0628\u0648\u0632\u0627\u062A", 85, 1020.000m, 12.000m, 60),
                new("\u0633\u0627\u0646\u062F\u0648\u064A\u0634", 45, 900.000m, 20.000m, 30)
            }, 3720.000m, 170);
        var result = builder.BuildSalesByCategoryReport(from, to, BusinessName, data);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A \u062D\u0633\u0628 \u0627\u0644\u0641\u0626\u0629");
        text.Should().Contain("2026/07/01");
        text.Should().Contain("2026/07/19");
        text.Should().Contain("19 \u064A\u0648\u0645");
        text.Should().Contain("1800.000");
        text.Should().Contain("1020.000");
        text.Should().Contain("900.000");
        text.Should().Contain("250");
        text.Should().Contain("3720.000");
        text.Should().Contain("\u062A\u0648\u0632\u064A\u0639 \u0627\u0644\u0625\u064A\u0631\u0627\u062F\u0627\u062A \u062D\u0633\u0628 \u0627\u0644\u0641\u0626\u0629");
        text.Should().Contain("170");
    }

    // ========================================================================
    // SaleReportBuilder — Sales By User
    // ========================================================================

    [Fact]
    public void BuildSalesByUserReport_WithData_ShowsEmployeePerformance()
    {
        var builder = new SaleReportBuilder();
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 19);
        var data = new SalesByUserReportData(
            new List<UserSalesDto>
            {
                new("\u0623\u062D\u0645\u062F", 45, 2150.000m, 47.778m, 25.000m, 2),
                new("\u0633\u0627\u0631\u0629", 60, 2890.000m, 48.167m, 30.000m, 3),
                new("\u0645\u062D\u0645\u062F", 38, 1680.000m, 44.211m, 10.000m, 1)
            }, 6720.000m, 143);
        var result = builder.BuildSalesByUserReport(from, to, BusinessName, data);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A \u062D\u0633\u0628 \u0627\u0644\u0645\u0648\u0638\u0641");
        text.Should().Contain("2150.000");
        text.Should().Contain("2890.000");
        text.Should().Contain("1680.000");
        text.Should().Contain("\u062A\u0631\u062A\u064A\u0628 \u0627\u0644\u0645\u0648\u0638\u0641\u064A\u0646 \u062D\u0633\u0628 \u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A");
        text.Should().Contain("3");
        text.Should().Contain("6720.000");
    }

    // ========================================================================
    // SaleReportBuilder — Sales By Payment Method
    // ========================================================================

    [Fact]
    public void BuildSalesByPaymentMethodReport_WithData_ContainsPaymentBreakdownAndComparison()
    {
        var builder = new SaleReportBuilder();
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 19);
        var data = new SalesByPaymentMethodReportData(
            new List<PaymentMethodSalesDto>
            {
                new("\u0646\u0642\u062F\u064A", 4200.000m, 70, 58.33m),
                new("\u0628\u0637\u0627\u0642\u0629 \u0627\u0626\u062A\u0645\u0627\u0646", 3000.000m, 45, 41.67m)
            }, 7200.000m, 115);
        var result = builder.BuildSalesByPaymentMethodReport(from, to, BusinessName, data);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A \u062D\u0633\u0628 \u0637\u0631\u064A\u0642\u0629 \u0627\u0644\u062F\u0641\u0639");
        text.Should().Contain("4200.000");
        text.Should().Contain("3000.000");
        text.Should().Contain("\u0645\u0642\u0627\u0631\u0646\u0629 \u0627\u0644\u0646\u0642\u062F\u064A \u0645\u0642\u0627\u0628\u0644 \u0627\u0644\u0628\u0637\u0627\u0642\u0629");
        text.Should().Contain("\u0627\u0644\u062A\u0648\u0632\u064A\u0639 \u0627\u0644\u0628\u064A\u0627\u0646\u064A");
        text.Should().Contain("7200.000");
        text.Should().Contain("115");
    }

    // ========================================================================
    // CashReportBuilder
    // ========================================================================

    [Fact]
    public void BuildCashReport_WithFullData_ContainsAllSections()
    {
        var builder = new CashReportBuilder();
        var data = new CashReportDto(500.000m, 512.350m, 12.350m, 850.000m, 420.000m, 45.000m, 200.000m, 100.000m);
        var shiftDate = new DateTime(2026, 7, 19);
        var shiftInfo = new ShiftInfoDto("SHIFT-001", "\u0623\u062D\u0645\u062F \u0627\u0644\u0643\u0627\u0634\u064A\u0631",
            new DateTime(2026, 7, 19, 8, 0, 0), new DateTime(2026, 7, 19, 16, 0, 0), 200.000m);
        var expenses = new List<CashExpenseEntry>
        {
            new(new DateTime(2026, 7, 19, 10, 30, 0), "\u062A\u0646\u0638\u064A\u0641", 25.000m),
            new(new DateTime(2026, 7, 19, 14, 0, 0), "\u0642\u0631\u0637\u0627\u0633\u064A\u0629", 20.000m)
        };
        var withdrawals = new List<CashWithdrawalEntry>
        {
            new(new DateTime(2026, 7, 19, 12, 0, 0), 150.000m, "\u0625\u064A\u062F\u0627\u0639 \u0628\u0646\u0643\u064A"),
        };
        var deposits = new List<CashDepositEntry>
        {
            new(new DateTime(2026, 7, 19, 15, 0, 0), 100.000m, "\u0625\u064A\u062F\u0627\u0639 \u0646\u0642\u062F\u064A"),
        };
        var returns = new List<CashReturnEntry>
        {
            new("RET-001", new DateTime(2026, 7, 19, 11, 0, 0), "\u0645\u0646\u062A\u062C \u062A\u0627\u0644\u0641", 20.000m),
        };

        var result = builder.BuildCashReport(data, BusinessName, shiftDate,
            shiftInfo, expenses, withdrawals, deposits, returns, "AdminUser");
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0646\u0642\u062F\u064A\u0629");
        text.Should().Contain("\u0645\u0639\u0644\u0648\u0645\u0627\u062A \u0627\u0644\u0648\u0631\u062F\u064A\u0629");
        text.Should().Contain("SHIFT-001");
        text.Should().Contain("200.000");
        text.Should().Contain("850.000");
        text.Should().Contain("420.000");
        text.Should().Contain("45.000");
        text.Should().Contain("885.000");
        text.Should().Contain("512.350");
        text.Should().Contain("\u0646\u0642\u0635");
        text.Should().Contain("372.650");
        text.Should().Contain("1270.000");
        text.Should().Contain("1250.000");
        text.Should().Contain("AdminUser");
    }

    [Fact]
    public void BuildCashReport_NoShiftInfoNoTransactions_ShowsEmptyMessages()
    {
        var builder = new CashReportBuilder();
        var data = new CashReportDto(0, 0, 0, 0, 0, 0, 0, 0);
        var result = builder.BuildCashReport(data, BusinessName, TestDate);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0646\u0642\u062F\u064A\u0629");
        text.Should().NotContain("\u0645\u0639\u0644\u0648\u0645\u0627\u062A \u0627\u0644\u0648\u0631\u062F\u064A\u0629");
        text.Should().Contain("0.000");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0645\u0635\u0631\u0648\u0641\u0627\u062A \u0645\u0633\u062C\u0644\u0629");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0633\u062D\u0648\u0628\u0627\u062A \u0645\u0633\u062C\u0644\u0629");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0625\u064A\u062F\u0627\u0639\u0627\u062A \u0645\u0633\u062C\u0644\u0629");
    }

    [Fact]
    public void BuildCashReport_WithSurplus_ShowsSurplusLabel()
    {
        var builder = new CashReportBuilder();
        var data = new CashReportDto(0, 100.000m, 100.000m, 50.000m, 0, 0, 0, 0);
        var result = builder.BuildCashReport(data, BusinessName, TestDate);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u0632\u064A\u0627\u062F\u0629");
        text.Should().Contain("50.000");
    }

    [Fact]
    public void BuildCashReport_WithDeficit_ShowsDeficitLabel()
    {
        var builder = new CashReportBuilder();
        var data = new CashReportDto(0, 80.000m, -20.000m, 100.000m, 0, 0, 0, 0);
        var result = builder.BuildCashReport(data, BusinessName, TestDate);
        var text = System.Text.Encoding.UTF8.GetString(result);

        text.Should().Contain("\u0646\u0642\u0635");
        text.Should().Contain("20.000");
    }

    // ========================================================================
    // InventoryReportBuilder — Current Stock Report
    // ========================================================================

    [Fact]
    public void BuildCurrentStockReport_WithMixedStock_ShowsAllSections()
    {
        // Arrange — items with: normal, low stock, and zero stock
        var builder = new InventoryReportBuilder();
        var items = new List<InventoryStatusDto>
        {
            new(Guid.NewGuid(), "\u0642\u0647\u0648\u0629", 50.000m, 5.000m, 45.000m, "\u0643\u062C\u0645", 10.000m, false),
            new(Guid.NewGuid(), "\u062D\u0644\u064A\u0628", 3.000m, 1.000m, 2.000m, "\u0644\u062A\u0631", 10.000m, true),
            new(Guid.NewGuid(), "\u062E\u0628\u0632", 0.000m, 0.000m, 0.000m, "\u0642\u0637\u0639\u0629", 5.000m, true)
        };
        var categorySummaries = new List<CategoryStockSummaryDto>
        {
            new("\u0645\u0634\u0631\u0648\u0628\u0627\u062A", 2, 53.000m, 265.000m),
            new("\u0645\u062E\u0628\u0648\u0632\u0627\u062A", 1, 0.000m, 0.000m)
        };
        var movementSummary = new StockMovementSummaryDto(100.000m, 80.000m, 5.000m, 2.000m, 3.000m);

        // Act
        var result = builder.BuildCurrentStockReport(items, BusinessName, categorySummaries, movementSummary);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — Header
        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u062E\u0632\u0648\u0646 \u0627\u0644\u062D\u0627\u0644\u064A");

        // Assert — Executive summary
        text.Should().Contain("\u0627\u0644\u0645\u0644\u062E\u0635 \u0627\u0644\u062A\u0646\u0641\u064A\u0630\u064A \u0644\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("3 \u0645\u0646\u062A\u062C"); // Total products
        text.Should().Contain("2 \u0645\u0646\u062A\u062C"); // 2 low stock + zero stock combined

        // Assert — Detailed stock table
        text.Should().Contain("\u062C\u062F\u0648\u0644 \u0627\u0644\u0645\u062E\u0632\u0648\u0646 \u0627\u0644\u062A\u0641\u0635\u064A\u0644\u064A");
        text.Should().Contain("\u0642\u0647\u0648\u0629");
        text.Should().Contain("\u062D\u0644\u064A\u0628");
        text.Should().Contain("\u062E\u0628\u0632");
        text.Should().Contain("50.000");
        text.Should().Contain("3.000");
        text.Should().Contain("0.000");

        // Assert — Low stock alerts section
        text.Should().Contain("\u062A\u0646\u0628\u064A\u0647\u0627\u062A \u0627\u0644\u0645\u062E\u0632\u0648\u0646 \u0627\u0644\u0645\u0646\u062E\u0641\u0636");
        text.Should().Contain("\u062D\u0644\u064A\u0628"); // low stock item
        text.Should().Contain("7.000"); // shortage = 10 - 3
        text.Should().Contain("\u0625\u062C\u0645\u0627\u0644\u064A \u0627\u0644\u0645\u0646\u062A\u062C\u0627\u062A \u0627\u0644\u0645\u0646\u062E\u0641\u0636\u0629: 2");

        // Assert — Zero stock items section
        text.Should().Contain("\u0627\u0644\u0645\u0646\u062A\u062C\u0627\u062A \u0627\u0644\u0646\u0627\u0641\u062F\u0629 \u0645\u0646 \u0627\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("\u062E\u0628\u0632");

        // Assert — Stock value summary
        text.Should().Contain("\u0642\u064A\u0645\u0629 \u0627\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("53.000"); // total quantity
        text.Should().Contain("47.000"); // total available (50-5 + 3-1 + 0 = 47)
        text.Should().Contain("6.000");  // total reserved (5+1+0 = 6)

        // Assert — Movement summary
        text.Should().Contain("\u0645\u0644\u062E\u0635 \u062D\u0631\u0643\u0627\u062A \u0627\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("100.000"); // PurchasesIn
        text.Should().Contain("80.000");  // SalesOut
        text.Should().Contain("5.000");   // ReturnsIn
        text.Should().Contain("2.000");   // WasteOut
        text.Should().Contain("3.000");   // Adjustments

        // Assert — Category summary
        text.Should().Contain("\u0645\u0644\u062E\u0635 \u0627\u0644\u0645\u062E\u0632\u0648\u0646 \u062D\u0633\u0628 \u0627\u0644\u0641\u0626\u0629");
        text.Should().Contain("53.000"); // category total qty
        text.Should().Contain("265.000"); // category total value

        // Assert — Footer
        text.Should().Contain("\u062A\u0645 \u0625\u0639\u062F\u0627\u062F \u0627\u0644\u062A\u0642\u0631\u064A\u0631 \u0641\u064A");
    }

    [Fact]
    public void BuildCurrentStockReport_AllHealthyStock_ShowsNoAlerts()
    {
        // Arrange — all items above minimum stock
        var builder = new InventoryReportBuilder();
        var items = new List<InventoryStatusDto>
        {
            new(Guid.NewGuid(), "\u0642\u0647\u0648\u0629", 50.000m, 0.000m, 50.000m, "\u0643\u062C\u0645", 10.000m, false),
            new(Guid.NewGuid(), "\u0634\u0627\u064A", 30.000m, 2.000m, 28.000m, "\u0643\u062C\u0645", 5.000m, false)
        };

        // Act — no category summaries, no movement summary
        var result = builder.BuildCurrentStockReport(items, BusinessName);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — Low stock and zero stock show success messages
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0645\u0646\u062A\u062C\u0627\u062A \u0628\u0645\u062E\u0632\u0648\u0646 \u0645\u0646\u062E\u0641\u0636");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0645\u0646\u062A\u062C\u0627\u062A \u0646\u0627\u0641\u062F\u0629 \u0645\u0646 \u0627\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0641\u0626\u0627\u062A \u0645\u062A\u0627\u062D\u0629");
    }

    // ========================================================================
    // InventoryReportBuilder — Movements Report
    // ========================================================================

    [Fact]
    public void BuildMovementsReport_WithMultipleMovements_ShowsAllTypes()
    {
        // Arrange
        var builder = new InventoryReportBuilder();
        var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
        var movements = new List<InventoryMovementDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "\u0642\u0647\u0648\u0629", "\u0634\u0631\u0627\u0621", 50.000m, 20.000m, 70.000m, "\u0648\u0635\u0648\u0644 \u0637\u0644\u0628\u064A\u0629", "\u0627\u0644\u0645\u062F\u064A\u0631", now, "PO-001"),
            new(Guid.NewGuid(), Guid.NewGuid(), "\u0642\u0647\u0648\u0629", "\u0628\u064A\u0639", -5.000m, 70.000m, 65.000m, "\u0641\u0627\u062A\u0648\u0631\u0629 #123", "\u0643\u0627\u0634\u064A\u0631", now.AddHours(1), "SALE-001"),
            new(Guid.NewGuid(), Guid.NewGuid(), "\u062D\u0644\u064A\u0628", "\u0645\u0631\u062A\u062C\u0639", 2.000m, 10.000m, 12.000m, "\u0645\u0646\u062A\u062C \u062A\u0627\u0644\u0641", "\u0643\u0627\u0634\u064A\u0631", now.AddHours(2), "RET-001"),
            new(Guid.NewGuid(), Guid.NewGuid(), "\u062E\u0628\u0632", "\u062A\u0627\u0644\u0641", -3.000m, 15.000m, 12.000m, "\u0627\u0646\u062A\u0647\u0627\u0621 \u0635\u0644\u0627\u062D\u064A\u0629", "\u0627\u0644\u0645\u062F\u064A\u0631", now.AddHours(3), null),
            new(Guid.NewGuid(), Guid.NewGuid(), "\u0642\u0647\u0648\u0629", "\u062A\u0633\u0648\u064A\u0629", -2.000m, 10.000m, 8.000m, "\u062C\u0631\u062F \u0645\u062E\u0632\u0648\u0646", "\u0627\u0644\u0645\u062F\u064A\u0631", now.AddHours(4), "ADJ-001")
        };

        // Act
        var result = builder.BuildMovementsReport(movements, BusinessName);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — Header
        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u062D\u0631\u0643\u0627\u062A \u0627\u0644\u0645\u062E\u0632\u0648\u0646");

        // Assert — Movement summary stats
        text.Should().Contain("\u0645\u0644\u062E\u0635 \u0627\u0644\u062D\u0631\u0643\u0627\u062A");
        text.Should().Contain("5 \u062D\u0631\u0643\u0629"); // total movements

        // Assert — Detailed movement table
        text.Should().Contain("\u062A\u0641\u0627\u0635\u064A\u0644 \u0627\u0644\u062D\u0631\u0643\u0627\u062A");
        text.Should().Contain("\u0642\u0647\u0648\u0629");
        text.Should().Contain("\u062D\u0644\u064A\u0628");
        text.Should().Contain("\u062E\u0628\u0632");
        text.Should().Contain("\u0634\u0631\u0627\u0621");
        text.Should().Contain("\u0628\u064A\u0639");
        text.Should().Contain("\u0645\u0631\u062A\u062C\u0639");
        text.Should().Contain("\u062A\u0627\u0644\u0641");
        text.Should().Contain("\u062A\u0633\u0648\u064A\u0629");
        text.Should().Contain("50.000");
        text.Should().Contain("70.000");
        text.Should().Contain("65.000");
        text.Should().Contain("12.000");
        text.Should().Contain("8.000");

        // Assert — Movements by type summary
        text.Should().Contain("\u0645\u0644\u062E\u0635 \u0627\u0644\u062D\u0631\u0643\u0627\u062A \u062D\u0633\u0628 \u0627\u0644\u0646\u0648\u0639");

        // Assert — Footer
        text.Should().Contain("\u062A\u0645 \u0625\u0639\u062F\u0627\u062F \u0627\u0644\u062A\u0642\u0631\u064A\u0631 \u0641\u064A");
    }

    [Fact]
    public void BuildMovementsReport_NoMovements_ShowsEmptySummary()
    {
        // Arrange — empty movements list
        var builder = new InventoryReportBuilder();
        var movements = new List<InventoryMovementDto>();

        // Act
        var result = builder.BuildMovementsReport(movements, BusinessName);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — header renders, no data sections
        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u062D\u0631\u0643\u0627\u062A \u0627\u0644\u0645\u062E\u0632\u0648\u0646");
        text.Should().Contain("0 \u062D\u0631\u0643\u0629"); // No movements
        text.Should().Contain("\u062A\u0641\u0627\u0635\u064A\u0644 \u0627\u0644\u062D\u0631\u0643\u0627\u062A");
        text.Should().Contain("\u0645\u0644\u062E\u0635 \u0627\u0644\u062D\u0631\u0643\u0627\u062A \u062D\u0633\u0628 \u0627\u0644\u0646\u0648\u0639");
    }

    // ========================================================================
    // ProfitabilityReportBuilder
    // ========================================================================

    [Fact]
    public void BuildProfitabilityReport_WithFullData_ContainsAllSections()
    {
        // Arrange
        var builder = new ProfitabilityReportBuilder();
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 19);
        var data = new ProfitabilityReportDto(
            TotalSales: 15000.000m,
            TotalCost: 9000.000m,
            GrossProfit: 6000.000m,
            ProfitMargin: 40.000m,
            TopProducts: new List<ProductProfitDto>
            {
                new("\u0642\u0647\u0648\u0629 \u0644\u0627\u062A\u064A\u0647", 4500.000m, 2250.000m, 2250.000m, 50.00m),
                new("\u0643\u0631\u0648\u0627\u0633\u0648\u0646", 3000.000m, 1800.000m, 1200.000m, 40.00m),
                new("\u0633\u0627\u0646\u062F\u0648\u064A\u0634", 2500.000m, 1750.000m, 750.000m, 30.00m)
            });
        var bottomProducts = new List<BottomProductDto>
        {
            new("\u0639\u0635\u064A\u0631 \u0645\u0634\u0643\u0644", 10.000m, 80.000m, 100.000m, -20.000m, -25.00m),
            new("\u0643\u0639\u0643\u0629", 5.000m, 30.000m, 35.000m, -5.000m, -16.67m)
        };
        var categoryProfits = new List<CategoryProfitDto>
        {
            new("\u0645\u0634\u0631\u0648\u0628\u0627\u062A", 8000.000m, 4400.000m, 3600.000m, 45.00m),
            new("\u0645\u062E\u0628\u0648\u0632\u0627\u062A", 4500.000m, 2700.000m, 1800.000m, 40.00m),
            new("\u0633\u0627\u0646\u062F\u0648\u064A\u0634\u0627\u062A", 2500.000m, 1900.000m, 600.000m, 24.00m)
        };
        var dailyProfits = new List<DailyProfitDto>
        {
            new(new DateTime(2026, 7, 1), 750.000m, 450.000m, 300.000m, 40.00m),
            new(new DateTime(2026, 7, 2), 820.000m, 500.000m, 320.000m, 39.02m),
            new(new DateTime(2026, 7, 3), 680.000m, 400.000m, 280.000m, 41.18m)
        };

        // Act
        var result = builder.BuildProfitabilityReport(data, BusinessName, from, to,
            bottomProducts, categoryProfits, dailyProfits);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — Header
        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0631\u0628\u062D\u064A\u0629");
        text.Should().Contain(BusinessName);
        text.Should().Contain("2026/07/01");
        text.Should().Contain("2026/07/19");

        // Assert — Executive summary
        text.Should().Contain("\u0627\u0644\u0645\u0644\u062E\u0635 \u0627\u0644\u062A\u0646\u0641\u064A\u0630\u064A");
        text.Should().Contain("15000.000"); // TotalSales
        text.Should().Contain("9000.000");  // TotalCost
        text.Should().Contain("6000.000");  // GrossProfit
        text.Should().Contain("40.000");    // ProfitMargin

        // Assert — Cost ratio analysis
        text.Should().Contain("\u062A\u062D\u0644\u064A\u0644 \u0646\u0633\u0628\u0629 \u0627\u0644\u062A\u0643\u0644\u0641\u0629");
        text.Should().Contain("\u0646\u0633\u0628\u0629 \u0627\u0644\u062A\u0643\u0644\u0641\u0629");
        text.Should().Contain("\u0646\u0633\u0628\u0629 \u0627\u0644\u0631\u0628\u062D");

        // Assert — Top products
        text.Should().Contain("\u0623\u0639\u0644\u0649 10 \u0645\u0646\u062A\u062C\u0627\u062A \u0631\u0628\u062D\u064A\u0629");
        text.Should().Contain("\u0642\u0647\u0648\u0629 \u0644\u0627\u062A\u064A\u0647");
        text.Should().Contain("\u0643\u0631\u0648\u0627\u0633\u0648\u0646");
        text.Should().Contain("\u0633\u0627\u0646\u062F\u0648\u064A\u0634");
        text.Should().Contain("2250.000"); // profit for top item
        text.Should().Contain("50.00%");

        // Assert — Bottom products
        text.Should().Contain("\u0623\u062F\u0646\u0649 10 \u0645\u0646\u062A\u062C\u0627\u062A \u0631\u0628\u062D\u064A\u0629");
        text.Should().Contain("\u0639\u0635\u064A\u0631 \u0645\u0634\u0643\u0644");
        text.Should().Contain("\u0643\u0639\u0643\u0629");
        text.Should().Contain("-20.000");  // negative profit
        text.Should().Contain("-25.00%");

        // Assert — Category profitability
        text.Should().Contain("\u0631\u0628\u062D\u064A\u0629 \u0627\u0644\u0641\u0626\u0627\u062A");
        text.Should().Contain("3600.000"); // category profit
        text.Should().Contain("45.00%");

        // Assert — Daily trend
        text.Should().Contain("\u0627\u0644\u0627\u062A\u062C\u0627\u0647 \u0627\u0644\u064A\u0648\u0645\u064A \u0644\u0644\u0631\u0628\u062D\u064A\u0629");
        text.Should().Contain("2026/07/01");
        text.Should().Contain("2026/07/02");
        text.Should().Contain("2026/07/03");
        text.Should().Contain("750.000");
        text.Should().Contain("820.000");
        text.Should().Contain("680.000");

        // Assert — Footer
        text.Should().Contain("\u062A\u0645 \u0625\u0639\u062F\u0627\u062F \u0627\u0644\u062A\u0642\u0631\u064A\u0631 \u0641\u064A");
    }

    [Fact]
    public void BuildProfitabilityReport_WithNoOptionalData_ShowsEmptyMessages()
    {
        // Arrange — no top products, no bottom products, no categories, no daily data
        var builder = new ProfitabilityReportBuilder();
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 19);
        var data = new ProfitabilityReportDto(0, 0, 0, 0, new List<ProductProfitDto>());

        // Act
        var result = builder.BuildProfitabilityReport(data, BusinessName, from, to);
        var text = System.Text.Encoding.UTF8.GetString(result);

        // Assert — Empty messages for all optional sections
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0645\u0646\u062A\u062C\u0627\u062A \u0645\u062A\u0627\u062D\u0629"); // top products
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0645\u0646\u062A\u062C\u0627\u062A \u0645\u062A\u0627\u062D\u0629"); // bottom products
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u0641\u0626\u0627\u062A \u0645\u062A\u0627\u062D\u0629"); // categories
        text.Should().Contain("\u0644\u0627 \u062A\u0648\u062C\u062F \u0628\u064A\u0627\u0646\u0627\u062A \u064A\u0648\u0645\u064A\u0629 \u0645\u062A\u0627\u062D\u0629"); // daily

        // Assert — Header and footer still render
        text.Should().Contain("\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0631\u0628\u062D\u064A\u0629");
        text.Should().Contain("\u062A\u0645 \u0625\u0639\u062F\u0627\u062F \u0627\u0644\u062A\u0642\u0631\u064A\u0631 \u0641\u064A");
    }
}
