using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for PurchaseOrderService (state machine, CRUD, inventory receive delegation)
/// and PurchaseOrderCalculator (line cost, total cost, status transitions, display text).
///
/// Test areas — PurchaseOrderService:
///   1. CreatePurchaseOrderAsync — supplier validation, item validation, order number gen, cost calc, audit
///   2. GetPurchaseOrderAsync — found / not found
///   3. GetPurchaseOrdersAsync — all / filtered by status
///   4. UpdatePurchaseOrderStatusAsync — success / PO not found
///   5. ReceivePurchaseOrderAsync — delegates to InventoryService
///
/// Test areas — PurchaseOrderCalculator:
///   6. ComputeLineCost — multiplication with JOD rounding
///   7. ComputeTotalCost — sum of items / null guard
///   8. IsValidTransition — all valid / invalid transitions
///   9. GetStatusDisplayText / GetStatusColorName — all known statuses
///  10. ComputeRemainingQuantity — edge cases
/// </summary>
public class PurchaseOrderServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultSupplierId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultInventoryItemId = Guid.NewGuid();
    private static readonly Guid DefaultPurchaseOrderId = Guid.NewGuid();

    private static Supplier CreateSupplier(Guid? id = null, string name = "مورد ABC")
    {
        return new Supplier
        {
            Id = id ?? DefaultSupplierId,
            Name = name,
            Phone = "+962-7-1234-5678",
            IsActive = true
        };
    }

    private static PurchaseOrder CreatePurchaseOrder(
        Guid? id = null,
        Guid? supplierId = null,
        string orderNumber = "PO-001",
        string status = "Pending",
        decimal totalAmount = 0m,
        Guid? userId = null)
    {
        return new PurchaseOrder
        {
            Id = id ?? DefaultPurchaseOrderId,
            SupplierId = supplierId ?? DefaultSupplierId,
            OrderNumber = orderNumber,
            Status = status,
            TotalAmount = totalAmount,
            UserId = userId ?? DefaultUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static PurchaseOrderItem CreatePurchaseOrderItem(
        Guid? id = null,
        Guid? purchaseOrderId = null,
        Guid? inventoryItemId = null,
        string itemName = "Test Item",
        decimal quantity = 10m,
        decimal unitCost = 5.500m,
        decimal receivedQuantity = 0m)
    {
        var qty = quantity;
        var cost = unitCost;
        var totalCost = POS.Domain.BusinessRules.MoneyPolicy.RoundToJOD(qty * cost);
        return new PurchaseOrderItem
        {
            Id = id ?? Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId ?? DefaultPurchaseOrderId,
            InventoryItemId = inventoryItemId ?? DefaultInventoryItemId,
            ItemName = itemName,
            Quantity = qty,
            UnitCost = cost,
            TotalCost = totalCost,
            ReceivedQuantity = receivedQuantity
        };
    }

    private static PurchaseOrderItemDto CreatePoItemDto(
        Guid? inventoryItemId = null,
        string itemName = "Test Item",
        decimal quantity = 10m,
        decimal unitCost = 5.500m)
    {
        return new PurchaseOrderItemDto(
            InventoryItemId: inventoryItemId ?? DefaultInventoryItemId,
            ItemName: itemName,
            Quantity: quantity,
            UnitCost: unitCost,
            TotalCost: 0m,
            ReceivedQuantity: 0m);
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a PurchaseOrderService with fully mocked dependencies.
    /// </summary>
    private (PurchaseOrderService service,
             Mock<IUnitOfWork> unitOfWorkMock,
             Mock<IAuditService> auditMock,
             Mock<IInventoryService> inventoryMock)
        BuildServiceWithMocks(
            Supplier? supplier = null,
            PurchaseOrder? purchaseOrder = null,
            List<PurchaseOrderItem>? poItems = null,
            List<PurchaseOrder>? allOrders = null,
            InventoryItem? inventoryItem = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();
        var inventoryMock = new Mock<IInventoryService>();

        // ---- Audit (fire-and-forget) ----
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // ---- Transaction / SaveChanges ----
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Suppliers ----
        var supplierRepoMock = new Mock<IRepository<Supplier>>();
        supplierRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(supplier);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(supplierRepoMock.Object);

        // ---- PurchaseOrders ----
        var poRepoMock = new Mock<IRepository<PurchaseOrder>>();
        poRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(purchaseOrder);
        poRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(allOrders ?? new List<PurchaseOrder>());
        poRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PurchaseOrder>()))
            .Returns(Task.CompletedTask);
        poRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrder>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(poRepoMock.Object);

        // ---- PurchaseOrderItems ----
        var poItemRepoMock = new Mock<IRepository<PurchaseOrderItem>>();
        poItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<PurchaseOrderItem, bool>>>()))
            .ReturnsAsync(poItems ?? new List<PurchaseOrderItem>());
        poItemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PurchaseOrderItem>()))
            .Returns(Task.CompletedTask);
        poItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrderItem>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(poItemRepoMock.Object);

        // ---- InventoryItems ----
        var invItemRepoMock = new Mock<IRepository<InventoryItem>>();
        invItemRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(inventoryItem);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(invItemRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.InventoryBatches).Returns(CreateEmptyRepoMock<InventoryBatch>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new PurchaseOrderService(
            unitOfWorkMock.Object,
            auditMock.Object,
            inventoryMock.Object);

        return (service, unitOfWorkMock, auditMock, inventoryMock);
    }

    // ========================================================================
    // CreatePurchaseOrderAsync Tests
    // ========================================================================

    [Fact]
    public async Task CreatePurchaseOrderAsync_SuccessfulCreation_ReturnsDto()
    {
        // Arrange
        var supplier = CreateSupplier();
        var items = new List<PurchaseOrderItemDto>
        {
            CreatePoItemDto(itemName: "مادة أ", quantity: 10m, unitCost: 5.500m),
            CreatePoItemDto(inventoryItemId: Guid.NewGuid(), itemName: "مادة ب", quantity: 5m, unitCost: 12.000m)
        };

        var (service, unitOfWorkMock, auditMock, _) = BuildServiceWithMocks(
            supplier: supplier,
            allOrders: new List<PurchaseOrder>());

        // Act
        var result = await service.CreatePurchaseOrderAsync(
            DefaultSupplierId, DefaultUserId, items, "ملاحظات: أمر شراء");

        // Assert
        result.Should().NotBeNull();
        result.OrderNumber.Should().Be("PO-001");
        result.SupplierName.Should().Be("مورد ABC");
        result.Status.Should().Be("Pending");
        result.Notes.Should().Be("ملاحظات: أمر شراء");

        // Total: (10 * 5.500 = 55.000) + (5 * 12.000 = 60.000) = 115.000
        result.TotalAmount.Should().Be(115.000m);

        // PO was added to repository
        unitOfWorkMock.Verify(u => u.PurchaseOrders.AddAsync(
            It.Is<PurchaseOrder>(po =>
                po.SupplierId == DefaultSupplierId &&
                po.Status == "Pending" &&
                po.Notes == "ملاحظات: أمر شراء")), Times.Once);

        // PO items were added (2 items)
        unitOfWorkMock.Verify(u => u.PurchaseOrderItems.AddAsync(
            It.IsAny<PurchaseOrderItem>()), Times.Exactly(2));

        // Order number generation used all orders
        var poRepo = Mock.Get(unitOfWorkMock.Object.PurchaseOrders);
        poRepo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);

        // PO was updated with total after items added
        unitOfWorkMock.Verify(u => u.PurchaseOrders.UpdateAsync(
            It.Is<PurchaseOrder>(po => po.TotalAmount == 115.000m)), Times.Once);

        // SaveChanges called twice (after initial add, after items + update)
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));

        // Audit was logged
        auditMock.Verify(a => a.LogAsync(
            DefaultUserId, AuditActionType.SettingChanged, "PurchaseOrder",
            It.IsAny<Guid>(), null,
            It.Is<string>(s => s.Contains("مورد ABC") && s.Contains("115") && s.Contains("2")),
            "ملاحظات: أمر شراء"), Times.Once);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_SupplierNotFound_ThrowsInvalidOperationException()
    {
        var (service, _, _, _) = BuildServiceWithMocks(supplier: null);
        var items = new List<PurchaseOrderItemDto> { CreatePoItemDto() };

        var act = () => service.CreatePurchaseOrderAsync(
            Guid.NewGuid(), DefaultUserId, items, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المورد غير موجود");
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_NullItems_ThrowsArgumentNullException()
    {
        var supplier = CreateSupplier();
        var (service, _, _, _) = BuildServiceWithMocks(supplier: supplier);

        var act = () => service.CreatePurchaseOrderAsync(
            DefaultSupplierId, DefaultUserId, null!, null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_EmptyItems_ThrowsInvalidOperationException()
    {
        var supplier = CreateSupplier();
        var (service, _, _, _) = BuildServiceWithMocks(supplier: supplier);

        var act = () => service.CreatePurchaseOrderAsync(
            DefaultSupplierId, DefaultUserId, new List<PurchaseOrderItemDto>(), null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("يجب إضافة بند واحد على الأقل");
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_IncrementsOrderNumber()
    {
        // Arrange — existing PO-005 means next should be PO-006
        var existingOrders = new List<PurchaseOrder>
        {
            CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-003"),
            CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-005")
        };

        var supplier = CreateSupplier();
        var items = new List<PurchaseOrderItemDto> { CreatePoItemDto() };

        var (service, _, _, _) = BuildServiceWithMocks(
            supplier: supplier,
            allOrders: existingOrders);

        // Act
        var result = await service.CreatePurchaseOrderAsync(
            DefaultSupplierId, DefaultUserId, items, null);

        // Assert
        result.OrderNumber.Should().Be("PO-006");
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_NoExistingOrders_StartsWithPO001()
    {
        var supplier = CreateSupplier();
        var items = new List<PurchaseOrderItemDto> { CreatePoItemDto() };

        var (service, _, _, _) = BuildServiceWithMocks(
            supplier: supplier,
            allOrders: new List<PurchaseOrder>());

        var result = await service.CreatePurchaseOrderAsync(
            DefaultSupplierId, DefaultUserId, items, null);

        result.OrderNumber.Should().Be("PO-001");
    }

    // ========================================================================
    // GetPurchaseOrderAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetPurchaseOrderAsync_ExistingOrder_ReturnsDto()
    {
        // Arrange
        var po = CreatePurchaseOrder(
            id: DefaultPurchaseOrderId,
            orderNumber: "PO-001",
            totalAmount: 150.000m);
        var poItems = new List<PurchaseOrderItem>
        {
            CreatePurchaseOrderItem(purchaseOrderId: DefaultPurchaseOrderId, itemName: "مادة أ", quantity: 10m, unitCost: 15.000m)
        };

        var (service, _, _, _) = BuildServiceWithMocks(
            purchaseOrder: po,
            poItems: poItems,
            supplier: CreateSupplier());

        // Act
        var result = await service.GetPurchaseOrderAsync(DefaultPurchaseOrderId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(DefaultPurchaseOrderId);
        result.OrderNumber.Should().Be("PO-001");
        result.SupplierName.Should().Be("مورد ABC");
        result.TotalAmount.Should().Be(150.000m);
        result.Items.Should().HaveCount(1);
        result.Items[0].ItemName.Should().Be("مادة أ");
    }

    [Fact]
    public async Task GetPurchaseOrderAsync_NonExistentOrder_ReturnsNull()
    {
        var (service, _, _, _) = BuildServiceWithMocks(purchaseOrder: null);
        var result = await service.GetPurchaseOrderAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ========================================================================
    // GetPurchaseOrdersAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetPurchaseOrdersAsync_NoFilter_ReturnsAllOrderedByCreatedAtDesc()
    {
        // Arrange
        var older = CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-001", status: "Pending");
        older.CreatedAt = DateTime.UtcNow.AddDays(-2);
        var newer = CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-002", status: "Received");
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var allOrders = new List<PurchaseOrder> { older, newer };

        var (service, _, _, _) = BuildServiceWithMocks(
            allOrders: allOrders,
            supplier: CreateSupplier());

        // Act
        var result = await service.GetPurchaseOrdersAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].OrderNumber.Should().Be("PO-002"); // newest first
        result[1].OrderNumber.Should().Be("PO-001");
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_WithStatusFilter_ReturnsFiltered()
    {
        var pending = CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-001", status: "Pending");
        var received = CreatePurchaseOrder(id: Guid.NewGuid(), orderNumber: "PO-002", status: "Received");

        var allOrders = new List<PurchaseOrder> { pending, received };

        var (service, _, _, _) = BuildServiceWithMocks(
            allOrders: allOrders,
            supplier: CreateSupplier());

        var result = await service.GetPurchaseOrdersAsync(status: "Received");

        result.Should().HaveCount(1);
        result[0].OrderNumber.Should().Be("PO-002");
        result[0].Status.Should().Be("Received");
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_NoOrders_ReturnsEmpty()
    {
        var (service, _, _, _) = BuildServiceWithMocks(allOrders: new List<PurchaseOrder>());
        var result = await service.GetPurchaseOrdersAsync();
        result.Should().BeEmpty();
    }

    // ========================================================================
    // UpdatePurchaseOrderStatusAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdatePurchaseOrderStatusAsync_Success_ReturnsSuccess()
    {
        var po = CreatePurchaseOrder(status: "Pending");
        var (service, unitOfWorkMock, _, _) = BuildServiceWithMocks(purchaseOrder: po);

        var result = await service.UpdatePurchaseOrderStatusAsync(
            DefaultPurchaseOrderId, "Received");

        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Be("تم تحديث حالة أمر الشراء بنجاح");

        po.Status.Should().Be("Received");
        unitOfWorkMock.Verify(u => u.PurchaseOrders.UpdateAsync(
            It.Is<PurchaseOrder>(p => p.Status == "Received")), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePurchaseOrderStatusAsync_NonExistentPO_ReturnsFailure()
    {
        var (service, _, _, _) = BuildServiceWithMocks(purchaseOrder: null);

        var result = await service.UpdatePurchaseOrderStatusAsync(
            Guid.NewGuid(), "Received");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("أمر الشراء غير موجود");
    }

    // ========================================================================
    // ReceivePurchaseOrderAsync Tests
    // ========================================================================

    [Fact]
    public async Task ReceivePurchaseOrderAsync_DelegatesToInventoryService()
    {
        var expectedResult = new OperationResult(true, SuccessMessage: "تم استلام أمر الشراء بنجاح");
        var (service, _, _, inventoryMock) = BuildServiceWithMocks();

        inventoryMock
            .Setup(i => i.ProcessPurchaseReceivedAsync(DefaultPurchaseOrderId, DefaultUserId))
            .ReturnsAsync(expectedResult);

        var result = await service.ReceivePurchaseOrderAsync(
            DefaultPurchaseOrderId, DefaultUserId);

        result.Should().BeSameAs(expectedResult);
        result.Success.Should().BeTrue();
        inventoryMock.Verify(i => i.ProcessPurchaseReceivedAsync(
            DefaultPurchaseOrderId, DefaultUserId), Times.Once);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_InventoryServiceFailure_ReturnsFailure()
    {
        var expectedResult = new OperationResult(false, ErrorMessage: "أمر الشراء غير موجود");
        var (service, _, _, inventoryMock) = BuildServiceWithMocks();

        inventoryMock
            .Setup(i => i.ProcessPurchaseReceivedAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(expectedResult);

        var result = await service.ReceivePurchaseOrderAsync(
            Guid.NewGuid(), DefaultUserId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("أمر الشراء غير موجود");
    }
}

// ========================================================================
// PurchaseOrderCalculator Tests (static class — no mocking needed)
// ========================================================================

public class PurchaseOrderCalculatorTests
{
    // ========================================================================
    // ComputeLineCost Tests
    // ========================================================================

    [Fact]
    public void ComputeLineCost_PositiveValues_MultipliesAndRoundsToThreeDecimals()
    {
        var cost = PurchaseOrderCalculator.ComputeLineCost(10m, 5.500m);
        cost.Should().Be(55.000m);
    }

    [Fact]
    public void ComputeLineCost_FractionalValues_RoundsCorrectly()
    {
        // 3 * 1.233333 = 3.699999 → rounded to 3.700
        var cost = PurchaseOrderCalculator.ComputeLineCost(3m, 1.233333m);
        cost.Should().Be(3.700m);
    }

    [Fact]
    public void ComputeLineCost_ZeroQuantity_ReturnsZero()
    {
        var cost = PurchaseOrderCalculator.ComputeLineCost(0m, 100.000m);
        cost.Should().Be(0m);
    }

    [Fact]
    public void ComputeLineCost_ZeroUnitCost_ReturnsZero()
    {
        var cost = PurchaseOrderCalculator.ComputeLineCost(50m, 0m);
        cost.Should().Be(0m);
    }

    // ========================================================================
    // ComputeTotalCost Tests
    // ========================================================================

    [Fact]
    public void ComputeTotalCost_MultipleItems_ReturnsSum()
    {
        var items = new List<PurchaseOrderItemDto>
        {
            new(Guid.NewGuid(), "مادة أ", 10m, 5.500m, 0m, 0m),
            new(Guid.NewGuid(), "مادة ب", 5m, 12.000m, 0m, 0m),
            new(Guid.NewGuid(), "مادة ج", 2m, 50.000m, 0m, 0m)
        };

        var total = PurchaseOrderCalculator.ComputeTotalCost(items);

        // (10 * 5.500 = 55) + (5 * 12 = 60) + (2 * 50 = 100) = 215
        total.Should().Be(215.000m);
    }

    [Fact]
    public void ComputeTotalCost_EmptyItems_ReturnsZero()
    {
        var total = PurchaseOrderCalculator.ComputeTotalCost(new List<PurchaseOrderItemDto>());
        total.Should().Be(0m);
    }

    [Fact]
    public void ComputeTotalCost_NullItems_ThrowsArgumentNullException()
    {
        var act = () => PurchaseOrderCalculator.ComputeTotalCost(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // IsValidTransition Tests
    // ========================================================================

    [Theory]
    [InlineData("Pending", "PartiallyReceived", true)]
    [InlineData("Pending", "Received", true)]
    [InlineData("Pending", "Cancelled", true)]
    [InlineData("PartiallyReceived", "Received", true)]
    [InlineData("PartiallyReceived", "Cancelled", true)]
    [InlineData("Received", "Pending", false)]         // Terminal — no going back
    [InlineData("Received", "Cancelled", false)]       // Terminal
    [InlineData("Cancelled", "Pending", false)]        // Terminal
    [InlineData("Cancelled", "Received", false)]       // Terminal
    [InlineData("Pending", "Pending", false)]          // Same status — no self-transition
    [InlineData("Received", "PartiallyReceived", false)]
    [InlineData("Cancelled", "PartiallyReceived", false)]
    [InlineData("PartiallyReceived", "Pending", false)]
    [InlineData("PartiallyReceived", "PartiallyReceived", false)]
    [InlineData("Pending", "Shipping", false)]         // Unknown status
    [InlineData("", "Pending", false)]                 // Empty current handled via switch catch-all
    [InlineData("Pending", "", false)]                 // Empty new handled via switch catch-all
    public void IsValidTransition_ReturnsCorrectResult(string current, string newStatus, bool expected)
    {
        var result = PurchaseOrderCalculator.IsValidTransition(current, newStatus);
        result.Should().Be(expected);
    }

    // ========================================================================
    // GetStatusDisplayText Tests
    // ========================================================================

    [Theory]
    [InlineData("Pending", "جديد")]
    [InlineData("PartiallyReceived", "مستلم جزئياً")]
    [InlineData("Received", "مستلم")]
    [InlineData("Cancelled", "ملغي")]
    [InlineData("Unknown", "غير معروف")]
    [InlineData("", "غير معروف")]
    public void GetStatusDisplayText_ReturnsCorrectText(string status, string expected)
    {
        var text = PurchaseOrderCalculator.GetStatusDisplayText(status);
        text.Should().Be(expected);
    }

    // ========================================================================
    // GetStatusColorName Tests
    // ========================================================================

    [Theory]
    [InlineData("Pending", "Info")]
    [InlineData("PartiallyReceived", "Warning")]
    [InlineData("Received", "Success")]
    [InlineData("Cancelled", "Error")]
    [InlineData("Unknown", "TextPrimary")]
    [InlineData("", "TextPrimary")]
    public void GetStatusColorName_ReturnsCorrectColor(string status, string expected)
    {
        var color = PurchaseOrderCalculator.GetStatusColorName(status);
        color.Should().Be(expected);
    }

    // ========================================================================
    // ComputeRemainingQuantity Tests
    // ========================================================================

    [Fact]
    public void ComputeRemainingQuantity_FullyReceived_ReturnsZero()
    {
        var remaining = PurchaseOrderCalculator.ComputeRemainingQuantity(10, 10);
        remaining.Should().Be(0);
    }

    [Fact]
    public void ComputeRemainingQuantity_PartiallyReceived_ReturnsDifference()
    {
        var remaining = PurchaseOrderCalculator.ComputeRemainingQuantity(10, 4);
        remaining.Should().Be(6);
    }

    [Fact]
    public void ComputeRemainingQuantity_NotReceived_ReturnsFullQuantity()
    {
        var remaining = PurchaseOrderCalculator.ComputeRemainingQuantity(10, 0);
        remaining.Should().Be(10);
    }

    [Fact]
    public void ComputeRemainingQuantity_OverReceived_ReturnsZero()
    {
        var remaining = PurchaseOrderCalculator.ComputeRemainingQuantity(10, 15);
        remaining.Should().Be(0);
    }

    [Fact]
    public void ComputeRemainingQuantity_ZeroOrdered_ReturnsZero()
    {
        var remaining = PurchaseOrderCalculator.ComputeRemainingQuantity(0, 0);
        remaining.Should().Be(0);
    }
}
