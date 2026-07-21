using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for KitchenOrderService — kitchen display integration,
/// order filtering, priority calculation, and station assignment.
///
/// Test areas:
///   1. GetPendingOrdersAsync — filtering by status, grouping, station mapping
///   2. Priority calculation — orders older than 30 minutes are priority
///   3. Table/Type display text — DineIn, Takeaway, Delivery
///   4. Edge cases — no active sales, no kitchen-assigned items
///   5. GetStationsAsync — active stations returned, inactive excluded
/// </summary>
public class KitchenOrderServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultSaleId = Guid.NewGuid();
    private static readonly Guid DefaultTableId = Guid.NewGuid();
    private static readonly Guid DefaultStationId = Guid.NewGuid();
    private static readonly Guid DefaultStationId2 = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    private static Sale CreateSale(
        Guid? id = null,
        string invoiceNumber = "INV-001",
        SaleStatus status = SaleStatus.Active,
        OrderType orderType = OrderType.DineIn,
        Guid? tableId = null,
        DateTime? createdAt = null,
        string? notes = null)
    {
        return new Sale
        {
            Id = id ?? DefaultSaleId,
            InvoiceNumber = invoiceNumber,
            Status = status,
            OrderType = orderType,
            TableId = tableId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Notes = notes,
            UserId = DefaultUserId,
            SubTotal = 100m,
            TotalAmount = 116m
        };
    }

    private static SaleItem CreateSaleItem(
        Guid? id = null,
        Guid? saleId = null,
        string productName = "برغر",
        Guid? kitchenStationId = null,
        decimal quantity = 1m,
        string? modifierSummary = null)
    {
        return new SaleItem
        {
            Id = id ?? Guid.NewGuid(),
            SaleId = saleId ?? DefaultSaleId,
            ProductId = Guid.NewGuid(),
            ProductName = productName,
            ProductArabicName = productName,
            KitchenStationId = kitchenStationId,
            Quantity = quantity,
            ModifierSummary = modifierSummary,
            UnitPrice = 25.000m,
            LineTotal = 25.000m
        };
    }

    private static Table CreateTable(Guid? id = null, string name = "1")
    {
        return new Table
        {
            Id = id ?? DefaultTableId,
            Name = name,
            RoomId = Guid.NewGuid(),
            Capacity = 4,
            Status = TableStatus.Available
        };
    }

    private static KitchenStation CreateStation(Guid? id = null, string name = "مطبخ رئيسي", bool isActive = true)
    {
        return new KitchenStation
        {
            Id = id ?? DefaultStationId,
            Name = name,
            IsActive = isActive
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Builds a KitchenOrderService with fully mocked IUnitOfWork.
    /// </summary>
    private (KitchenOrderService service, Mock<IUnitOfWork> unitOfWorkMock)
        BuildServiceWithMocks(
            List<Sale>? sales = null,
            List<SaleItem>? saleItems = null,
            List<Table>? tables = null,
            List<KitchenStation>? stations = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        // ---- Sales ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(sales ?? new List<Sale>());
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- SaleItems ----
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(saleItems ?? new List<SaleItem>());
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // ---- Tables ----
        var tableRepoMock = new Mock<IRepository<Table>>();
        tableRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(tables ?? new List<Table>());
        unitOfWorkMock.Setup(u => u.Tables).Returns(tableRepoMock.Object);

        // ---- KitchenStations ----
        var stationRepoMock = new Mock<IRepository<KitchenStation>>();
        stationRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(stations ?? new List<KitchenStation>());
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(stationRepoMock.Object);

        // ---- Stub remaining repos ----
        var emptyRepoMock = new Mock<IRepository<User>>();
        emptyRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        unitOfWorkMock.Setup(u => u.Users).Returns(emptyRepoMock.Object);

        var service = new KitchenOrderService(unitOfWorkMock.Object);

        return (service, unitOfWorkMock);
    }

    // ========================================================================
    // GetPendingOrdersAsync — Basic Tests
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_ActiveSaleWithKitchenItems_ReturnsOrder()
    {
        // Arrange
        var sale = CreateSale(invoiceNumber: "INV-001", status: SaleStatus.Active, tableId: DefaultTableId);
        var table = CreateTable(name: "5");
        var station = CreateStation(name: "مطبخ اللحوم");
        var item = CreateSaleItem(saleId: sale.Id, productName: "ستيك", kitchenStationId: DefaultStationId);

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            tables: new List<Table> { table },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert
        result.Should().HaveCount(1);
        var order = result[0];
        order.OrderNumber.Should().Be("INV-001");
        order.Station.Should().Be("مطبخ اللحوم");
        order.TableOrType.Should().Be("طاولة 5");
        order.Items.Should().HaveCount(1);
        order.Items[0].Name.Should().Be("ستيك");
        order.Items[0].Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task GetPendingOrdersAsync_HeldSale_AlsoIncluded()
    {
        // Arrange
        var activeSale = CreateSale(id: Guid.NewGuid(), invoiceNumber: "INV-001", status: SaleStatus.Active);
        var heldSale = CreateSale(id: Guid.NewGuid(), invoiceNumber: "INV-002", status: SaleStatus.Held);

        var item1 = CreateSaleItem(saleId: activeSale.Id, productName: "برغر", kitchenStationId: DefaultStationId);
        var item2 = CreateSaleItem(saleId: heldSale.Id, productName: "بيتزا", kitchenStationId: DefaultStationId);

        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { activeSale, heldSale },
            saleItems: new List<SaleItem> { item1, item2 },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert — both Active and Held sales included
        result.Should().HaveCount(2);
        result.Select(o => o.OrderNumber).Should().Contain(new[] { "INV-001", "INV-002" });
    }

    [Theory]
    [InlineData(SaleStatus.Completed)]
    [InlineData(SaleStatus.Cancelled)]
    [InlineData(SaleStatus.Returned)]
    public async Task GetPendingOrdersAsync_NonPendingStatuses_Excluded(SaleStatus status)
    {
        // Arrange — only a non-pending sale exists
        var sale = CreateSale(status: status);
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingOrdersAsync_NoActiveSales_ReturnsEmpty()
    {
        var (service, _) = BuildServiceWithMocks(sales: new List<Sale>());

        var result = await service.GetPendingOrdersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingOrdersAsync_AllItemsWithoutStation_ReturnsEmpty()
    {
        // Arrange — sale items have no KitchenStationId
        var sale = CreateSale();
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: null);

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert — no items with KitchenStationId → no kitchen orders
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingOrdersAsync_OrdersOrderedByTimeAscending()
    {
        // Arrange — two orders, older first
        var oldSale = CreateSale(
            id: Guid.NewGuid(), invoiceNumber: "INV-001",
            createdAt: DateTime.UtcNow.AddMinutes(-40));
        var newSale = CreateSale(
            id: Guid.NewGuid(), invoiceNumber: "INV-002",
            createdAt: DateTime.UtcNow.AddMinutes(-10));

        var oldItem = CreateSaleItem(saleId: oldSale.Id, kitchenStationId: DefaultStationId);
        var newItem = CreateSaleItem(saleId: newSale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { newSale, oldSale },  // newSale first in list
            saleItems: new List<SaleItem> { oldItem, newItem },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert — ordered by time ascending: old first, new second
        result.Should().HaveCount(2);
        result[0].OrderNumber.Should().Be("INV-001"); // older
        result[1].OrderNumber.Should().Be("INV-002"); // newer
    }

    // ========================================================================
    // GetPendingOrdersAsync — Priority Calculation
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_OrderOlderThan30Minutes_IsPriority()
    {
        // Arrange — order created 45 minutes ago
        var sale = CreateSale(invoiceNumber: "INV-001", createdAt: DateTime.UtcNow.AddMinutes(-45));
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].IsPriority.Should().BeTrue();
    }

    [Fact]
    public async Task GetPendingOrdersAsync_OrderNewerThan30Minutes_NotPriority()
    {
        // Arrange — order created 15 minutes ago
        var sale = CreateSale(invoiceNumber: "INV-001", createdAt: DateTime.UtcNow.AddMinutes(-15));
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].IsPriority.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingOrdersAsync_Under30Minutes_NotPriority()
    {
        // Arrange — use a fixed reference time to avoid execution-time drift
        var now = DateTime.UtcNow;
        var sale = CreateSale(invoiceNumber: "INV-001", createdAt: now); // 0 min ago = clearly under 30
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert — just created, clearly less than 30 min
        result.Should().HaveCount(1);
        result[0].IsPriority.Should().BeFalse();
    }

    // ========================================================================
    // GetPendingOrdersAsync — Table/Order Type Display
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_DineInWithTable_ShowsTableName()
    {
        var sale = CreateSale(orderType: OrderType.DineIn, tableId: DefaultTableId);
        var table = CreateTable(name: "7");
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            tables: new List<Table> { table },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        result.Should().HaveCount(1);
        result[0].TableOrType.Should().Be("طاولة 7");
    }

    [Fact]
    public async Task GetPendingOrdersAsync_DineInWithoutTable_FallsBackToTable()
    {
        // When TableId is null for a DineIn order
        var sale = CreateSale(orderType: OrderType.DineIn, tableId: null);
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        result.Should().HaveCount(1);
        result[0].TableOrType.Should().Be("طاولة");
    }

    [Fact]
    public async Task GetPendingOrdersAsync_Takeaway_ShowsSafari()
    {
        var sale = CreateSale(orderType: OrderType.Takeaway);
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        result.Should().HaveCount(1);
        result[0].TableOrType.Should().Be("سفري");
    }

    [Fact]
    public async Task GetPendingOrdersAsync_Delivery_ShowsTawseel()
    {
        var sale = CreateSale(orderType: OrderType.Delivery);
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId);
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        result.Should().HaveCount(1);
        result[0].TableOrType.Should().Be("توصيل");
    }

    // ========================================================================
    // GetPendingOrdersAsync — Station Name Lookup
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_StationNotFound_FallsBackToMainKitchen()
    {
        // Arrange — no station in the stations list matches the item's KitchenStationId
        var sale = CreateSale();
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: Guid.NewGuid()); // different ID

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation>());  // empty stations

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Station.Should().Be("المطبخ الرئيسي");
    }

    [Fact]
    public async Task GetPendingOrdersAsync_MultipleSalesOnDifferentStations_ReturnsSeparateOrders()
    {
        // Arrange — two separate sales, each linked to a different kitchen station
        var station1 = CreateStation(id: DefaultStationId, name: "مطبخ اللحوم");
        var station2 = CreateStation(id: DefaultStationId2, name: "مطبخ البيتزا");

        // Use explicit timestamps to guarantee ordering
        var olderTime = DateTime.UtcNow.AddMinutes(-5);
        var newerTime = DateTime.UtcNow;

        var sale1 = CreateSale(id: Guid.NewGuid(), invoiceNumber: "INV-001", createdAt: olderTime);
        var sale2 = CreateSale(id: Guid.NewGuid(), invoiceNumber: "INV-002", createdAt: newerTime);

        var item1 = CreateSaleItem(saleId: sale1.Id, productName: "ستيك", kitchenStationId: DefaultStationId);
        var item2 = CreateSaleItem(saleId: sale2.Id, productName: "بيتزا", kitchenStationId: DefaultStationId2);

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale1, sale2 },
            saleItems: new List<SaleItem> { item1, item2 },
            stations: new List<KitchenStation> { station1, station2 });

        // Act
        var result = await service.GetPendingOrdersAsync();

        // Assert — ordered by time ascending, each order shows its own station
        result.Should().HaveCount(2);
        result[0].Station.Should().Be("مطبخ اللحوم");  // older sale first
        result[1].Station.Should().Be("مطبخ البيتزا"); // newer sale second
    }

    // ========================================================================
    // GetPendingOrdersAsync — Modifier Summary and Notes
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_ItemWithModifierSummary_IncludesInDto()
    {
        var sale = CreateSale(notes: "بدون بصل");
        var item = CreateSaleItem(saleId: sale.Id, kitchenStationId: DefaultStationId, modifierSummary: "بدون بصل، جبنة إضافية");
        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { item },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        result.Should().HaveCount(1);
        result[0].Notes.Should().Be("بدون بصل");
        result[0].Items[0].ModifierSummary.Should().Be("بدون بصل، جبنة إضافية");
    }

    // ========================================================================
    // GetPendingOrdersAsync — Mixed Items (some kitchen, some not)
    // ========================================================================

    [Fact]
    public async Task GetPendingOrdersAsync_MixedWithAndWithoutStation_FiltersCorrectly()
    {
        var sale = CreateSale();
        var kitchenItem = CreateSaleItem(
            saleId: sale.Id, productName: "برغر",
            kitchenStationId: DefaultStationId);
        var nonKitchenItem = CreateSaleItem(
            id: Guid.NewGuid(), saleId: sale.Id,
            productName: "مشروب", kitchenStationId: null);

        var station = CreateStation();

        var (service, _) = BuildServiceWithMocks(
            sales: new List<Sale> { sale },
            saleItems: new List<SaleItem> { kitchenItem, nonKitchenItem },
            stations: new List<KitchenStation> { station });

        var result = await service.GetPendingOrdersAsync();

        // Only kitchen item appears in order
        result.Should().HaveCount(1);
        result[0].Items.Should().HaveCount(1);
        result[0].Items[0].Name.Should().Be("برغر");
    }

    // ========================================================================
    // GetStationsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetStationsAsync_ActiveStations_ReturnsNames()
    {
        var stations = new List<KitchenStation>
        {
            CreateStation(id: Guid.NewGuid(), name: "مطبخ اللحوم", isActive: true),
            CreateStation(id: Guid.NewGuid(), name: "مطبخ المعجنات", isActive: true),
            CreateStation(id: Guid.NewGuid(), name: "مطبخ البيتزا", isActive: false) // inactive
        };

        var (service, _) = BuildServiceWithMocks(stations: stations);

        var result = await service.GetStationsAsync();

        result.Should().HaveCount(2);
        result.Should().Contain("مطبخ اللحوم");
        result.Should().Contain("مطبخ المعجنات");
        result.Should().NotContain("مطبخ البيتزا");
    }

    [Fact]
    public async Task GetStationsAsync_NoActiveStations_ReturnsEmpty()
    {
        var stations = new List<KitchenStation>
        {
            CreateStation(isActive: false),
            CreateStation(isActive: false)
        };

        var (service, _) = BuildServiceWithMocks(stations: stations);

        var result = await service.GetStationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStationsAsync_NoStations_ReturnsEmpty()
    {
        var (service, _) = BuildServiceWithMocks(stations: new List<KitchenStation>());
        var result = await service.GetStationsAsync();
        result.Should().BeEmpty();
    }
}
