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
/// Unit tests for DashboardService covering GetWidgetsAsync — all 5 dashboard widgets:
/// Today's Sales, Active Shift Info, Low Stock Count, Pending Kitchen Orders, Recent Transactions.
/// </summary>
public class DashboardServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultProductId = Guid.NewGuid();
    private static readonly Guid DefaultCategoryId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    private static Sale CreateCompletedSale(
        Guid? saleId = null,
        decimal total = 100.000m,
        Guid? userId = null,
        DateTime? createdAt = null)
    {
        return new Sale
        {
            Id = saleId ?? Guid.NewGuid(),
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            TotalAmount = total,
            SubTotal = total,
            TaxAmount = 0,
            DiscountAmount = 0,
            Status = SaleStatus.Completed,
            IsPaid = true,
            UserId = userId ?? DefaultUserId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    private static Sale CreateActiveSale(Guid? saleId = null)
    {
        return new Sale
        {
            Id = saleId ?? Guid.NewGuid(),
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            TotalAmount = 50.000m,
            Status = SaleStatus.Active,
            IsPaid = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Shift CreateOpenShift(
        Guid? shiftId = null,
        Guid? userId = null,
        decimal totalSales = 0)
    {
        return new Shift
        {
            Id = shiftId ?? Guid.NewGuid(),
            ShiftNumber = 1,
            UserId = userId ?? DefaultUserId,
            RegisterId = Guid.NewGuid(),
            OpeningCash = 500.000m,
            TotalSales = totalSales,
            Status = ShiftStatus.Open,
            OpenedAt = DateTime.UtcNow.AddHours(-4)
        };
    }

    private static Product CreateTestProduct(
        Guid? id = null,
        string arabicName = "منتج",
        decimal minStock = 5m,
        ProductStatus status = ProductStatus.Active,
        Guid? kitchenStationId = null)
    {
        return new Product
        {
            Id = id ?? DefaultProductId,
            ArabicName = arabicName,
            Name = "Product",
            Sku = "SKU-001",
            Price = 10.000m,
            MinStock = minStock,
            Status = status,
            KitchenStationId = kitchenStationId,
            CategoryId = DefaultCategoryId
        };
    }

    private static InventoryItem CreateInventory(Guid productId, decimal quantity = 50m)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            ReservedQuantity = 0
        };
    }

    private static SaleItem CreateSaleItem(Guid saleId, Guid productId, decimal quantity = 1m)
    {
        return new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            ProductName = "منتج",
            Quantity = quantity,
            UnitPrice = 10.000m,
            LineTotal = quantity * 10.000m
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

    private (DashboardService service, Mock<IUnitOfWork> uowMock)
        BuildServiceWithMocks(
            List<Sale>? sales = null,
            List<Shift>? shifts = null,
            List<Product>? products = null,
            List<InventoryItem>? inventory = null,
            List<SaleItem>? saleItems = null)
    {
        var uowMock = new Mock<IUnitOfWork>();

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(sales ?? new List<Sale>());
        saleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Sale, bool>>>()))
            .ReturnsAsync((Expression<Func<Sale, bool>> predicate) =>
                (sales ?? new List<Sale>()).AsQueryable().Where(predicate).ToList());
        uowMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Shifts repository ----
        var shiftRepoMock = new Mock<IRepository<Shift>>();
        shiftRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Shift, bool>>>()))
            .ReturnsAsync((Expression<Func<Shift, bool>> predicate) =>
                (shifts ?? new List<Shift>()).AsQueryable().Where(predicate).ToList());
        uowMock.Setup(u => u.Shifts).Returns(shiftRepoMock.Object);

        // ---- Products repository ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(products ?? new List<Product>());
        uowMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- InventoryItems repository ----
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(inventory ?? new List<InventoryItem>());
        uowMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // ---- SaleItems repository ----
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(saleItems ?? new List<SaleItem>());
        uowMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        uowMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);

        var service = new DashboardService(uowMock.Object);
        return (service, uowMock);
    }

    /// <summary>
    /// Custom mock builder for tests that need the Payments repository configured.
    /// </summary>
    private static Mock<IUnitOfWork> CreateDashboardUowWithPayments(
        List<Sale>? sales = null,
        List<Payment>? payments = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Sales repository
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(sales ?? new List<Sale>());
        uowMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // Payments repository
        var paymentRepoMock = new Mock<IRepository<Payment>>();
        paymentRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(payments ?? new List<Payment>());
        uowMock.Setup(u => u.Payments).Returns(paymentRepoMock.Object);

        // Stub remaining repos
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        uowMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        uowMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);

        return uowMock;
    }

    // ========================================================================
    // GetWidgetsAsync — Dashboard Widgets
    // ========================================================================

    [Fact]
    public async Task GetWidgetsAsync_WithTodaySales_ShowsTodaySalesWidget()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 150.000m, createdAt: today.AddHours(10)),
            CreateCompletedSale(total: 50.000m, createdAt: today.AddHours(12)),
            CreateCompletedSale(total: 200.000m, createdAt: today.AddDays(-1)) // yesterday, not today
        };

        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — 5 widgets expected
        result.Should().HaveCount(5);

        // Widget 1: Today's Sales
        var todayWidget = result[0];
        todayWidget.Title.Should().Be("مبيعات اليوم");
        todayWidget.Value.Should().Be("200.000 JOD"); // 150 + 50
        todayWidget.Description.Should().Be("2 عملية بيع");
        todayWidget.IsAlert.Should().BeFalse();
    }

    [Fact]
    public async Task GetWidgetsAsync_NoTodaySales_ShowsZero()
    {
        // Arrange — no sales at all
        var (service, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert
        var todayWidget = result[0];
        todayWidget.Title.Should().Be("مبيعات اليوم");
        todayWidget.Value.Should().Be("0.000 JOD");
        todayWidget.IsAlert.Should().BeTrue(); // zero sales → alert
    }

    [Fact]
    public async Task GetWidgetsAsync_WithOpenShift_ShowsShiftInfo()
    {
        // Arrange
        var shift = CreateOpenShift(totalSales: 300.000m);
        var (service, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift });

        // Act
        var result = await service.GetWidgetsAsync(shift.UserId);

        // Assert — Widget 2: Active Shift
        var shiftWidget = result[1];
        shiftWidget.Title.Should().Be("الوردية الحالية");
        shiftWidget.Value.Should().Contain("300.000");
        shiftWidget.IsAlert.Should().BeFalse();
    }

    [Fact]
    public async Task GetWidgetsAsync_NoOpenShift_ShowsNoShiftWarning()
    {
        // Arrange — no open shifts for this user
        var (service, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 2: No shift warning
        var shiftWidget = result[1];
        shiftWidget.Title.Should().Be("الوردية");
        shiftWidget.Value.Should().Be("لا توجد وردية مفتوحة");
        shiftWidget.IsAlert.Should().BeTrue();
    }

    [Fact]
    public async Task GetWidgetsAsync_LowStock_ShowsAlert()
    {
        // Arrange
        var productLow = CreateTestProduct(minStock: 10m, arabicName: "منتج منخفض");
        var productOk = CreateTestProduct(id: Guid.NewGuid(), minStock: 5m, arabicName: "منتج جيد");
        var inventory = new List<InventoryItem>
        {
            CreateInventory(productLow.Id, quantity: 3m),   // 3 <= 10 → low
            CreateInventory(productOk.Id, quantity: 50m)    // 50 > 5 → ok
        };

        var (service, _) = BuildServiceWithMocks(
            products: new List<Product> { productLow, productOk },
            inventory: inventory);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 3: Low Stock
        var stockWidget = result[2];
        stockWidget.Title.Should().Be("مخزون منخفض");
        stockWidget.Value.Should().Be("1");
        stockWidget.IsAlert.Should().BeTrue();
    }

    [Fact]
    public async Task GetWidgetsAsync_NoLowStock_ShowsGood()
    {
        // Arrange
        var product = CreateTestProduct(minStock: 5m);
        var inventory = new List<InventoryItem>
        {
            CreateInventory(product.Id, quantity: 50m)
        };

        var (service, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventory: inventory);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 3: No low stock
        var stockWidget = result[2];
        stockWidget.Title.Should().Be("مخزون منخفض");
        stockWidget.Value.Should().Be("0");
        stockWidget.IsAlert.Should().BeFalse();
    }

    [Fact]
    public async Task GetWidgetsAsync_WithKitchenOrders_ShowsPendingCount()
    {
        // Arrange — a product assigned to a kitchen station
        var kitchenProductId = Guid.NewGuid();
        var sale = CreateActiveSale();

        var products = new List<Product>
        {
            CreateTestProduct(id: kitchenProductId, arabicName: "وجبة", kitchenStationId: Guid.NewGuid()),
            CreateTestProduct(id: Guid.NewGuid(), arabicName: "مشروب", kitchenStationId: null) // not kitchen
        };

        var saleItems = new List<SaleItem>
        {
            CreateSaleItem(sale.Id, kitchenProductId, quantity: 2m),  // kitchen item
            CreateSaleItem(sale.Id, products[1].Id, quantity: 1m)     // beverage, not counted
        };

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            products: products,
            saleItems: saleItems);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 4: Kitchen Orders
        var kitchenWidget = result[3];
        kitchenWidget.Title.Should().Be("طلبات المطبخ");
        kitchenWidget.Value.Should().Be("1"); // 1 kitchen SaleItem
        kitchenWidget.IsAlert.Should().BeTrue();
    }

    [Fact]
    public async Task GetWidgetsAsync_WithRecentTransactions_ShowsLast5()
    {
        // Arrange — 7 completed sales, should show last 5
        var sales = Enumerable.Range(1, 7)
            .Select(i => CreateCompletedSale(total: i * 10m, createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 5: Recent Transactions (last 5 of 7)
        var recentWidget = result[4];
        recentWidget.Title.Should().Be("آخر المعاملات");
        recentWidget.Value.Should().Be("5"); // last 5 only
        recentWidget.Description.Should().Contain("INV-");
        recentWidget.IsAlert.Should().BeFalse();
    }

    [Fact]
    public async Task GetWidgetsAsync_NoCompletedSales_ShowsNoTransactions()
    {
        // Arrange — only active sales, no completed
        var sales = new List<Sale>
        {
            CreateActiveSale()
        };

        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetWidgetsAsync(DefaultUserId);

        // Assert — Widget 5: No recent transactions
        var recentWidget = result[4];
        recentWidget.Title.Should().Be("آخر المعاملات");
        recentWidget.Value.Should().Be("0");
        recentWidget.Description.Should().Be("لا توجد معاملات حديثة");
    }

    // ========================================================================
    // GetRecentTransactionsAsync — Dedicated Method
    // ========================================================================

    [Fact]
    public async Task GetRecentTransactionsAsync_ReturnsRecentCompletedSales()
    {
        // Arrange — 10 completed sales, should return only the last 5
        var sales = Enumerable.Range(1, 10)
            .Select(i => CreateCompletedSale(total: i * 10m, createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetRecentTransactionsAsync(5);

        // Assert — most recent (i=1, UtcNow-1min) has total 10m, 5th most recent (i=5) has total 50m
        result.Should().HaveCount(5);
        result[0].TotalAmount.Should().Be(10m);
        result[4].TotalAmount.Should().Be(50m);
        result.Should().BeInDescendingOrder(r => r.Date);
    }

    [Fact]
    public async Task GetRecentTransactionsAsync_WithPayments_IncludesPaymentMethod()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(saleId: saleId, total: 100.000m)
        };
        var payments = new List<Payment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                Amount = 100.000m,
                PaymentMethod = PaymentMethod.Cash,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Build with payment mock setup
        var uowMock = CreateDashboardUowWithPayments(sales, payments);
        var service = new DashboardService(uowMock.Object);

        // Act
        var result = await service.GetRecentTransactionsAsync(5);

        // Assert
        result.Should().HaveCount(1);
        result[0].PaymentMethod.Should().Be("Cash");
        result[0].TotalAmount.Should().Be(100.000m);
    }

    [Fact]
    public async Task GetRecentTransactionsAsync_NoCompletedSales_ReturnsEmpty()
    {
        // Arrange — only active sale
        var sales = new List<Sale> { CreateActiveSale() };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetRecentTransactionsAsync(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentTransactionsAsync_LessThanCount_ReturnsAll()
    {
        // Arrange — only 2 completed sales, requesting 5
        var sales = new List<Sale>
        {
            CreateCompletedSale(total: 50.000m, createdAt: DateTime.UtcNow.AddHours(-1)),
            CreateCompletedSale(total: 25.000m, createdAt: DateTime.UtcNow.AddHours(-2))
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetRecentTransactionsAsync(5);

        // Assert — returns all 2 even though count requested is 5
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentTransactionsAsync_SaleWithoutPayment_ShowsDash()
    {
        // Arrange — completed sale with no payments
        var saleId = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(saleId: saleId, total: 75.000m)
        };
        var (service, _) = BuildServiceWithMocks(sales: sales);

        // Act
        var result = await service.GetRecentTransactionsAsync(5);

        // Assert — payment method should be "—" (dash) when no payment exists
        result.Should().HaveCount(1);
        result[0].PaymentMethod.Should().Be("—");
    }
}
