#nullable enable

using System.Linq.Expressions;
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
/// Unit tests for TableService covering all 7 public methods:
/// GetTablesAsync, GetRoomsAsync, AddTableAsync, UpdateTableStatusAsync,
/// OpenTableAsync, CloseTableAsync, TransferOrderAsync.
/// </summary>
public class TableServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid Room1Id = Guid.NewGuid();
    private static readonly Guid Room2Id = Guid.NewGuid();

    private static Table CreateTestTable(
        Guid? id = null,
        string name = "T1",
        Guid? roomId = null,
        int capacity = 4,
        TableStatus status = TableStatus.Available,
        Guid? orderId = null)
    {
        return new Table
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            RoomId = roomId ?? Room1Id,
            Capacity = capacity,
            Status = status,
            CurrentOrderId = orderId
        };
    }

    private static Room CreateTestRoom(
        Guid? id = null,
        string name = "صالة رئيسية",
        int sortOrder = 1)
    {
        return new Room
        {
            Id = id ?? Room1Id,
            Name = name,
            SortOrder = sortOrder
        };
    }

    private static Sale CreateTestSale(Guid saleId, Guid tableId)
    {
        return new Sale
        {
            Id = saleId,
            TableId = tableId,
            InvoiceNumber = $"INV-{saleId:N}",
            Status = SaleStatus.Active,
            IsPaid = false
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

    private (TableService service, Mock<IUnitOfWork> uowMock, Mock<IAuditService> auditMock)
        BuildServiceWithMocks(
            List<Table>? tables = null,
            List<Room>? rooms = null,
            List<Sale>? sales = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Tables repository ----
        var tableRepoMock = new Mock<IRepository<Table>>();
        tableRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(tables ?? new List<Table>());
        tableRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => tables?.FirstOrDefault(t => t.Id == id));
        tableRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Table, bool>>>()))
            .ReturnsAsync((Expression<Func<Table, bool>> predicate) =>
                (tables ?? new List<Table>()).AsQueryable().Where(predicate).ToList());
        tableRepoMock.Setup(r => r.AddAsync(It.IsAny<Table>())).Returns(Task.CompletedTask);
        tableRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Table>())).Returns(Task.CompletedTask);
        uowMock.Setup(u => u.Tables).Returns(tableRepoMock.Object);

        // ---- Rooms repository ----
        var roomRepoMock = new Mock<IRepository<Room>>();
        roomRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(rooms ?? new List<Room>());
        roomRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => rooms?.FirstOrDefault(r => r.Id == id));
        uowMock.Setup(u => u.Rooms).Returns(roomRepoMock.Object);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Sale, bool>>>()))
            .ReturnsAsync((Expression<Func<Sale, bool>> predicate) =>
                (sales ?? new List<Sale>()).AsQueryable().Where(predicate).ToList());
        uowMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        uowMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
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

        var service = new TableService(uowMock.Object, auditMock.Object);
        return (service, uowMock, auditMock);
    }

    // ========================================================================
    // GetTablesAsync — Table List with Room Names
    // ========================================================================

    [Fact]
    public async Task GetTablesAsync_ReturnsTablesWithRoomNames()
    {
        // Arrange
        var room1Id = Room1Id;
        var room2Id = Room2Id;
        var tables = new List<Table>
        {
            CreateTestTable(name: "T1", roomId: room1Id),
            CreateTestTable(name: "T2", roomId: room2Id),
            CreateTestTable(name: "T3", roomId: Guid.NewGuid()) // no matching room
        };
        var rooms = new List<Room>
        {
            CreateTestRoom(room1Id, name: "صالة رئيسية"),
            CreateTestRoom(room2Id, name: "صالة خاصة")
        };
        var (service, _, _) = BuildServiceWithMocks(tables, rooms);

        // Act
        var result = await service.GetTablesAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].RoomName.Should().Be("صالة رئيسية");
        result[1].RoomName.Should().Be("صالة خاصة");
        result[2].RoomName.Should().BeNull(); // no matching room
    }

    // ========================================================================
    // GetRoomsAsync — Room List Ordered by SortOrder
    // ========================================================================

    [Fact]
    public async Task GetRoomsAsync_ReturnsRoomsOrderedBySortOrder()
    {
        // Arrange
        var rooms = new List<Room>
        {
            CreateTestRoom(Room1Id, name: "صالة ب", sortOrder: 2),
            CreateTestRoom(Room2Id, name: "صالة أ", sortOrder: 1)
        };
        var (service, _, _) = BuildServiceWithMocks(rooms: rooms);

        // Act
        var result = await service.GetRoomsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("صالة أ");
        result[1].Name.Should().Be("صالة ب");
    }

    [Fact]
    public async Task GetRoomsAsync_Empty_ReturnsEmptyList()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetRoomsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // AddTableAsync — Add New Table
    // ========================================================================

    [Fact]
    public async Task AddTableAsync_Success_AddsTable()
    {
        // Arrange
        var room = CreateTestRoom(Room1Id, name: "صالة رئيسية");
        var (service, uowMock, _) = BuildServiceWithMocks(rooms: new List<Room> { room });

        // Act
        var result = await service.AddTableAsync("T5", Room1Id, 6);

        // Assert
        result.Name.Should().Be("T5");
        result.RoomName.Should().Be("صالة رئيسية");
        result.Capacity.Should().Be(6);
        result.Status.Should().Be("Available");

        uowMock.Verify(u => u.Tables.AddAsync(
            It.Is<Table>(t =>
                t.Name == "T5" &&
                t.RoomId == Room1Id &&
                t.Capacity == 6 &&
                t.Status == TableStatus.Available)), Times.Once);

        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddTableAsync_DuplicateNameInRoom_Throws()
    {
        // Arrange
        var tables = new List<Table>
        {
            CreateTestTable(name: "T1", roomId: Room1Id)
        };
        var (service, _, _) = BuildServiceWithMocks(tables);

        // Act
        var act = () => service.AddTableAsync("T1", Room1Id, 4);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("رقم الطاولة موجود بالفعل في هذه الغرفة");
    }

    [Fact]
    public async Task AddTableAsync_SameNameDifferentRoom_Succeeds()
    {
        // Arrange
        var room2 = CreateTestRoom(Room2Id, name: "صالة أخرى");
        var tables = new List<Table>
        {
            CreateTestTable(name: "T1", roomId: Room1Id)
        };
        var (service, uowMock, _) = BuildServiceWithMocks(tables, new List<Room> { room2 });

        // Act
        var result = await service.AddTableAsync("T1", Room2Id, 4);

        // Assert — same name but different room is valid
        result.Name.Should().Be("T1");
        uowMock.Verify(u => u.Tables.AddAsync(It.IsAny<Table>()), Times.Once);
    }

    [Fact]
    public async Task AddTableAsync_NoRoomId_Succeeds()
    {
        // Arrange
        var (service, uowMock, _) = BuildServiceWithMocks();

        // Act
        var result = await service.AddTableAsync("T1", null, 2);

        // Assert
        result.Name.Should().Be("T1");
        uowMock.Verify(u => u.Tables.AddAsync(
            It.Is<Table>(t => t.RoomId == Guid.Empty)), Times.Once);
    }

    // ========================================================================
    // UpdateTableStatusAsync — Change Table Status
    // ========================================================================

    [Fact]
    public async Task UpdateTableStatusAsync_Success_UpdatesStatus()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var table = CreateTestTable(tableId, status: TableStatus.Available, name: "T1");
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.UpdateTableStatusAsync(tableId, "Occupied");

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Contain("تم تغيير حالة الطاولة");
        table.Status.Should().Be(TableStatus.Occupied);

        uowMock.Verify(u => u.Tables.UpdateAsync(table), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateTableStatusAsync_TableNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.UpdateTableStatusAsync(Guid.NewGuid(), "Occupied");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة غير موجودة");
    }

    [Fact]
    public async Task UpdateTableStatusAsync_InvalidStatus_ReturnsFailure()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var table = CreateTestTable(tableId);
        var (service, _, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.UpdateTableStatusAsync(tableId, "InvalidStatus");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("حالة غير صالحة");
    }

    // ========================================================================
    // OpenTableAsync — Open/Assign Table
    // ========================================================================

    [Fact]
    public async Task OpenTableAsync_Success_OpensTable()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var table = CreateTestTable(tableId, status: TableStatus.Available, name: "T1");
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.OpenTableAsync(tableId, orderId);

        // Assert
        result.Success.Should().BeTrue();
        table.Status.Should().Be(TableStatus.Occupied);
        table.CurrentOrderId.Should().Be(orderId);

        uowMock.Verify(u => u.Tables.UpdateAsync(table), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task OpenTableAsync_TableNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.OpenTableAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة غير موجودة");
    }

    [Fact]
    public async Task OpenTableAsync_TableOccupied_ReturnsFailure()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var table = CreateTestTable(tableId, status: TableStatus.Occupied);
        var (service, _, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.OpenTableAsync(tableId, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة ليست متاحة");
    }

    // ========================================================================
    // CloseTableAsync — Close/Release Table
    // ========================================================================

    [Fact]
    public async Task CloseTableAsync_Success_ClosesTable()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var table = CreateTestTable(tableId, status: TableStatus.Occupied, orderId: Guid.NewGuid());
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.CloseTableAsync(tableId);

        // Assert
        result.Success.Should().BeTrue();
        table.Status.Should().Be(TableStatus.Available);
        table.CurrentOrderId.Should().BeNull();

        uowMock.Verify(u => u.Tables.UpdateAsync(table), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CloseTableAsync_TableNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.CloseTableAsync(Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة غير موجودة");
    }

    [Fact]
    public async Task CloseTableAsync_AlreadyAvailable_ReturnsFailure()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var table = CreateTestTable(tableId, status: TableStatus.Available);
        var (service, _, _) = BuildServiceWithMocks(new List<Table> { table });

        // Act
        var result = await service.CloseTableAsync(tableId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة مفتوحة بالفعل");
    }

    // ========================================================================
    // TransferOrderAsync — Transfer Between Tables
    // ========================================================================

    [Fact]
    public async Task TransferOrderAsync_Success_TransfersOrder()
    {
        // Arrange
        var fromTableId = Guid.NewGuid();
        var toTableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sale = CreateTestSale(orderId, fromTableId);

        var fromTable = CreateTestTable(fromTableId, name: "T1", status: TableStatus.Occupied, orderId: orderId);
        var toTable = CreateTestTable(toTableId, name: "T2", status: TableStatus.Available);

        var (service, uowMock, auditMock) = BuildServiceWithMocks(
            tables: new List<Table> { fromTable, toTable },
            sales: new List<Sale> { sale });

        // Act
        var result = await service.TransferOrderAsync(fromTableId, toTableId);

        // Assert
        result.Success.Should().BeTrue();

        // From table cleared
        fromTable.Status.Should().Be(TableStatus.Available);
        fromTable.CurrentOrderId.Should().BeNull();

        // To table now occupied with the order
        toTable.Status.Should().Be(TableStatus.Occupied);
        toTable.CurrentOrderId.Should().Be(orderId);

        // Sale table reference updated
        sale.TableId.Should().Be(toTableId);

        uowMock.Verify(u => u.Tables.UpdateAsync(fromTable), Times.Once);
        uowMock.Verify(u => u.Tables.UpdateAsync(toTable), Times.Once);
        uowMock.Verify(u => u.Sales.UpdateAsync(sale), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        auditMock.Verify(a => a.LogAsync(null, AuditActionType.PriceChange, "Table", fromTableId,
            It.Is<string>(s => s.Contains(orderId.ToString())),
            It.Is<string>(s => s.Contains(toTableId.ToString())),
            "Order transferred between tables"), Times.Once);
    }

    [Fact]
    public async Task TransferOrderAsync_SameTable_ReturnsFailure()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.TransferOrderAsync(tableId, tableId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("لا يمكن نقل الطلب إلى نفس الطاولة");
    }

    [Fact]
    public async Task TransferOrderAsync_SourceTableNoOrder_ReturnsFailure()
    {
        // Arrange
        var fromTableId = Guid.NewGuid();
        var toTableId = Guid.NewGuid();
        var fromTable = CreateTestTable(fromTableId, status: TableStatus.Available, orderId: null);
        var toTable = CreateTestTable(toTableId, status: TableStatus.Available);

        var (service, _, _) = BuildServiceWithMocks(
            tables: new List<Table> { fromTable, toTable });

        // Act
        var result = await service.TransferOrderAsync(fromTableId, toTableId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("لا يوجد طلب على الطاولة المصدر");
    }

    [Fact]
    public async Task TransferOrderAsync_DestinationOccupied_ReturnsFailure()
    {
        // Arrange
        var fromTableId = Guid.NewGuid();
        var toTableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var fromTable = CreateTestTable(fromTableId, name: "T1", status: TableStatus.Occupied, orderId: orderId);
        var toTable = CreateTestTable(toTableId, name: "T2", status: TableStatus.Occupied);

        var (service, _, _) = BuildServiceWithMocks(
            tables: new List<Table> { fromTable, toTable });

        // Act
        var result = await service.TransferOrderAsync(fromTableId, toTableId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة الوجهة غير متاحة");
    }

    [Fact]
    public async Task TransferOrderAsync_SourceTableNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.TransferOrderAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة المصدر غير موجودة");
    }

    [Fact]
    public async Task TransferOrderAsync_DestinationTableNotFound_ReturnsFailure()
    {
        // Arrange
        var fromTableId = Guid.NewGuid();
        var fromTable = CreateTestTable(fromTableId, name: "T1", status: TableStatus.Occupied, orderId: Guid.NewGuid());

        var (service, _, _) = BuildServiceWithMocks(
            tables: new List<Table> { fromTable });

        // Act
        var result = await service.TransferOrderAsync(fromTableId, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطاولة الوجهة غير موجودة");
    }
}
