#nullable enable

using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services.Implementations;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for ReportService covering all 4 public methods:
/// GetSalesReportAsync, GetInventoryReportAsync, GetProfitabilityReportAsync, GetDailySalesAsync.
/// </summary>
public class ReportServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultCategoryId = Guid.NewGuid();
    private static readonly Guid DefaultProductId = Guid.NewGuid();

    private static Sale CreateCompletedSale(
        Guid? saleId = null,
        decimal total = 100.000m,
        decimal tax = 16.000m,
        decimal discount = 0m,
        Guid? userId = null,
        Guid? categoryId = null,
        DateTime? createdAt = null)
    {
        return new Sale
        {
            Id = saleId ?? Guid.NewGuid(),
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            TotalAmount = total,
            SubTotal = total - tax + discount,
            TaxAmount = tax,
            DiscountAmount = discount,
            Status = SaleStatus.Completed,
            IsPaid = true,
            UserId = userId ?? DefaultUserId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    private static SaleItem CreateSaleItem(
        Guid saleId,
        Guid productId,
        decimal quantity = 1m,
        decimal unitPrice = 10.000m,
        decimal cost = 5.000m,
        string productName = "منتج")
    {
        return new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Cost = cost,
            LineTotal = quantity * unitPrice
        };
    }

    private static Product CreateTestProduct(
        Guid? id = null,
        string arabicName = "منتج",
        decimal minStock = 5m,
        decimal price = 10.000m,
        string unit = "piece",
        ProductStatus status = ProductStatus.Active,
        Guid? categoryId = null)
    {
        return new Product
        {
            Id = id ?? DefaultProductId,
            ArabicName = arabicName,
            Name = "Product",
            Sku = "SKU-001",
            Price = price,
            MinStock = minStock,
            Unit = unit,
            Status = status,
            CategoryId = categoryId ?? DefaultCategoryId
        };
    }

    private static InventoryItem CreateInventory(Guid productId, decimal quantity = 50m, decimal reserved = 0m)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            ReservedQuantity = reserved
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync(new List<T>());
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    private (ReportService service, Mock<IUnitOfWork> uowMock)
        BuildServiceWithMocks(
            List<Sale>? sales = null,
            List<SaleItem>? saleItems = null,
            List<Payment>? payments = null,
            List<Product>? products = null,
            List<InventoryItem>? inventory = null)
    {
        var uowMock = new Mock<IUnitOfWork>();

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(sales ?? new List<Sale>());
        uowMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- SaleItems repository ----
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(saleItems ?? new List<SaleItem>());
        uowMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // ---- Payments repository ----
        var paymentRepoMock = new Mock<IRepository<Payment>>();
        paymentRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(payments ?? new List<Payment>());
        uowMock.Setup(u => u.Payments).Returns(paymentRepoMock.Object);

        // ---- Products repository ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(products ?? new List<Product>());
        productRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate) =>
                (products ?? new List<Product>()).AsQueryable().Where(predicate).ToList());
        uowMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- InventoryItems repository ----
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(inventory ?? new List<InventoryItem>());
        uowMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        uowMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        uowMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        uowMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        uowMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        uowMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        uowMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        uowMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        uowMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        uowMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        uowMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new ReportService(uowMock.Object);
        return (service, uowMock);
    }

    // ========================================================================
    // GetSalesReportAsync — Sales Report with Filtering
    // ========================================================================

    [Fact]
    public async Task GetSalesReportAsync_NoFilter_ReturnsAllCompleted()
    {
        // Arrange
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 100.000m),
            CreateCompletedSale(total: 50.000m),
            CreateCompletedSale(total: 200.000m, saleId: Guid.NewGuid(), categoryId: Guid.NewGuid())
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        var filter = new SalesReportFilter(null, null, null, null, null);

        // Act
        var result = await service.GetSalesReportAsync(filter);

        // Assert
        result.TotalTransactions.Should().Be(3);
        result.GrandTotal.Should().Be(350.000m);
    }

    [Fact]
    public async Task GetSalesReportAsync_FilterByDate_RestrictsRange()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 100.000m, createdAt: today.AddDays(-5)),
            CreateCompletedSale(total: 200.000m, createdAt: today),
            CreateCompletedSale(total: 300.000m, createdAt: today.AddDays(5))
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        var filter = new SalesReportFilter(today.AddDays(-1), today.AddDays(1), null, null, null);

        // Act
        var result = await service.GetSalesReportAsync(filter);

        // Assert — only today's sale (200) within range
        result.TotalTransactions.Should().Be(1);
        result.GrandTotal.Should().Be(200.000m);
    }

    [Fact]
    public async Task GetSalesReportAsync_FilterByUser_RestrictsToUser()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(userId: DefaultUserId, total: 100.000m),
            CreateCompletedSale(userId: otherUserId, total: 200.000m)
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        var filter = new SalesReportFilter(null, null, DefaultUserId, null, null);

        // Act
        var result = await service.GetSalesReportAsync(filter);

        // Assert — only DefaultUserId's sale
        result.TotalTransactions.Should().Be(1);
        result.GrandTotal.Should().Be(100.000m);
    }

    [Fact]
    public async Task GetSalesReportAsync_GroupsByDay()
    {
        // Arrange — 2 sales today, 1 yesterday
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 100.000m, createdAt: today.AddHours(10)),
            CreateCompletedSale(total: 50.000m, createdAt: today.AddHours(14)),
            CreateCompletedSale(total: 200.000m, createdAt: yesterday)
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        var filter = new SalesReportFilter(null, null, null, null, null);

        // Act
        var result = await service.GetSalesReportAsync(filter);

        // Assert — 2 daily groups
        result.DailySales.Should().HaveCount(2);
        result.DailySales.Should().Contain(d => d.Date == today && d.TransactionCount == 2 && d.TotalSales == 150.000m);
        result.DailySales.Should().Contain(d => d.Date == yesterday && d.TransactionCount == 1 && d.TotalSales == 200.000m);
    }

    [Fact]
    public async Task GetSalesReportAsync_NoCompletedSales_ReturnsZeroTotals()
    {
        // Arrange — only active sales, none completed
        var activeSale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-ACTIVE",
            TotalAmount = 50.000m,
            Status = SaleStatus.Active,
            IsPaid = false
        };
        var (service, _) = BuildServiceWithMocks(sales: new List<Sale> { activeSale });

        var filter = new SalesReportFilter(null, null, null, null, null);

        // Act
        var result = await service.GetSalesReportAsync(filter);

        // Assert
        result.TotalTransactions.Should().Be(0);
        result.GrandTotal.Should().Be(0m);
        result.DailySales.Should().BeEmpty();
    }

    // ========================================================================
    // GetInventoryReportAsync — Current Stock Status
    // ========================================================================

    [Fact]
    public async Task GetInventoryReportAsync_ReturnsStockStatusWithLowStockFlags()
    {
        // Arrange
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var products = new List<Product>
        {
            CreateTestProduct(product1Id, arabicName: "قهوة", minStock: 10m),
            CreateTestProduct(product2Id, arabicName: "شاي", minStock: 10m)
        };
        var inventory = new List<InventoryItem>
        {
            CreateInventory(product1Id, quantity: 50m),
            CreateInventory(product2Id, quantity: 5m)  // low stock (5 <= 10)
        };

        var (service, _) = BuildServiceWithMocks(products: products, inventory: inventory);

        // Act
        var result = await service.GetInventoryReportAsync();

        // Assert
        result.TotalItems.Should().Be(2);
        result.LowStockCount.Should().Be(1);
        result.Items.Should().Contain(i => i.IsLowStock);
        result.Items.First(i => i.IsLowStock).ProductName.Should().Be("شاي");
    }

    [Fact]
    public async Task GetInventoryReportAsync_ArchivedProducts_Excluded()
    {
        // Arrange
        var archivedProduct = CreateTestProduct(
            Guid.NewGuid(), arabicName: "قديم",
            status: ProductStatus.Archived);
        var (service, _) = BuildServiceWithMocks(
            products: new List<Product> { archivedProduct });

        // Act
        var result = await service.GetInventoryReportAsync();

        // Assert — archived products excluded
        result.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task GetInventoryReportAsync_NoInventoryRecord_ShowsZeroQty()
    {
        // Arrange — product exists but no inventory
        var product = CreateTestProduct();
        var (service, _) = BuildServiceWithMocks(
            products: new List<Product> { product });

        // Act
        var result = await service.GetInventoryReportAsync();

        // Assert
        result.TotalItems.Should().Be(1);
        result.Items.First().AvailableQuantity.Should().Be(0);
        result.Items.First().IsLowStock.Should().BeTrue(); // 0 <= minStock 5
    }

    // ========================================================================
    // GetProfitabilityReportAsync — Profit and Margin Calculations
    // ========================================================================

    [Fact]
    public async Task GetProfitabilityReportAsync_CalculatesProfitCorrectly()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(saleId, total: 100.000m)
        };
        var saleItems = new List<SaleItem>
        {
            CreateSaleItem(saleId, DefaultProductId, quantity: 10m, unitPrice: 10.000m, cost: 4.000m, productName: "منتج أ")
        };

        var (service, _) = BuildServiceWithMocks(sales: sales, saleItems: saleItems);

        // Act
        var result = await service.GetProfitabilityReportAsync(null, null);

        // Assert
        // Sales = 10 * 10 = 100, Cost = 10 * 4 = 40, Profit = 60, Margin = 60%
        result.TotalSales.Should().Be(100.000m);
        result.TotalCost.Should().Be(40.000m);
        result.GrossProfit.Should().Be(60.000m);
        result.ProfitMargin.Should().Be(60.000m);

        result.TopProducts.Should().HaveCount(1);
        result.TopProducts[0].ProductName.Should().Be("منتج أ");
        result.TopProducts[0].Profit.Should().Be(60.000m);
        result.TopProducts[0].Margin.Should().Be(60.000m);
    }

    [Fact]
    public async Task GetProfitabilityReportAsync_NoSales_ReturnsZero()
    {
        // Arrange — no completed sales
        var (service, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetProfitabilityReportAsync(null, null);

        // Assert
        result.TotalSales.Should().Be(0);
        result.TotalCost.Should().Be(0);
        result.GrossProfit.Should().Be(0);
        result.ProfitMargin.Should().Be(0);
        result.TopProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfitabilityReportAsync_WithDateRange_FiltersSales()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var saleInRangeId = Guid.NewGuid();
        var saleOutOfRangeId = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(saleInRangeId, total: 100.000m, createdAt: today),
            CreateCompletedSale(saleOutOfRangeId, total: 500.000m, createdAt: today.AddMonths(-6))
        };
        var saleItems = new List<SaleItem>
        {
            CreateSaleItem(saleInRangeId, DefaultProductId, quantity: 10m, unitPrice: 10.000m, cost: 5.000m)
        };

        var (service, _) = BuildServiceWithMocks(sales: sales, saleItems: saleItems);

        // Act — filter to last month
        var result = await service.GetProfitabilityReportAsync(today.AddDays(-30), today);

        // Assert — only 1 sale in range
        result.TotalSales.Should().Be(100.000m);
        result.TopProducts.Should().HaveCount(1);
    }

    // ========================================================================
    // GetDailySalesAsync — Daily Sales for Date Range
    // ========================================================================

    [Fact]
    public async Task GetDailySalesAsync_ReturnsGroupedByDay()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 100.000m, createdAt: today),
            CreateCompletedSale(total: 50.000m, createdAt: today),
            CreateCompletedSale(total: 200.000m, createdAt: yesterday)
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act — filter to this week
        var result = await service.GetDailySalesAsync(today.AddDays(-7), today);

        // Assert
        result.Should().HaveCount(2);
        result.First(d => d.Date == today).TotalSales.Should().Be(150.000m);
        result.First(d => d.Date == today).TransactionCount.Should().Be(2);
        result.First(d => d.Date == yesterday).TotalSales.Should().Be(200.000m);
        result.First(d => d.Date == yesterday).TransactionCount.Should().Be(1);

        // Ordered by date
        result[0].Date.Should().Be(yesterday);
        result[1].Date.Should().Be(today);
    }

    [Fact]
    public async Task GetDailySalesAsync_OutsideDateRange_Excluded()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 100.000m, createdAt: today),
            CreateCompletedSale(total: 50.000m, createdAt: today.AddDays(-10))
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act — narrow range (today only)
        var result = await service.GetDailySalesAsync(today, today);

        // Assert
        result.Should().HaveCount(1);
        result[0].TotalSales.Should().Be(100.000m);
    }
}
