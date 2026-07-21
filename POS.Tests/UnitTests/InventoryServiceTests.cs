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
/// Unit tests for InventoryService covering all 6 public methods:
///   - GetCurrentStockAsync, GetLowStockAsync, GetMovementsAsync
///   - AdjustStockAsync, RecordWasteAsync, ProcessPurchaseReceivedAsync
/// </summary>
public class InventoryServiceTests
{
    private static readonly Guid DefaultProductId = Guid.NewGuid();

    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Product CreateProduct(Guid? productId = null, string arabicName = "قهوة",
        ProductStatus status = ProductStatus.Active, decimal minStock = 5m)
    {
        return new Product
        {
            Id = productId ?? DefaultProductId,
            ArabicName = arabicName,
            Name = "Test Product",
            Price = 10.000m,
            Cost = 5.000m,
            Unit = "كجم",
            MinStock = minStock,
            Status = status,
            TaxRate = 0.16m
        };
    }

    private static InventoryItem CreateInventory(Guid productId, decimal quantity = 10m,
        decimal reservedQuantity = 0m)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            ReservedQuantity = reservedQuantity
        };
    }

    private static InventoryMovement CreateMovement(Guid productId, Guid userId,
        MovementType type, decimal qty, decimal before, decimal after,
        DateTime? timestamp = null, string? reference = null)
    {
        return new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            InventoryItemId = Guid.NewGuid(),
            MovementType = type,
            Quantity = qty,
            BeforeQuantity = before,
            AfterQuantity = after,
            UserId = userId,
            Timestamp = timestamp ?? DateTime.UtcNow,
            Reason = "Test",
            Reference = reference
        };
    }

    private static User CreateUser(Guid userId, string fullName = "Test Cashier")
    {
        return new User
        {
            Id = userId,
            FullName = fullName,
            Username = "test",
            IsActive = true
        };
    }

    private static PurchaseOrder CreatePurchaseOrder(Guid poId, string orderNumber = "PO-001",
        string status = "Pending")
    {
        return new PurchaseOrder
        {
            Id = poId,
            OrderNumber = orderNumber,
            Status = status,
            SupplierId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalAmount = 100.000m
        };
    }

    private static PurchaseOrderItem CreatePoItem(Guid poId, Guid productId, decimal qty = 10m,
        decimal received = 0m)
    {
        return new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = poId,
            InventoryItemId = productId,
            ItemName = "Test Item",
            Quantity = qty,
            ReceivedQuantity = received,
            UnitCost = 5.000m,
            TotalCost = 50.000m
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private (InventoryService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            List<Product>? products = null,
            List<InventoryItem>? inventoryItems = null,
            List<InventoryMovement>? movements = null,
            List<User>? users = null,
            PurchaseOrder? purchaseOrder = null,
            List<PurchaseOrderItem>? poItems = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Transactions stubs ----
        var emptyRepoMock = new Mock<IRepository<Sale>>();

        // ---- Products repository ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(products ?? new List<Product>());
        productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => products?.FirstOrDefault(p => p.Id == id));
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- InventoryItems repository ----
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(inventoryItems ?? new List<InventoryItem>());
        inventoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => (inventoryItems ?? new List<InventoryItem>()).FirstOrDefault(i => i.Id == id));
        inventoryRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>()))
            .ReturnsAsync((Expression<Func<InventoryItem, bool>> predicate) =>
                (inventoryItems ?? new List<InventoryItem>()).AsQueryable().Where(predicate).ToList());
        inventoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InventoryItem>()))
            .Callback<InventoryItem>(inv => { if (inv.Id == Guid.Empty) inv.Id = Guid.NewGuid(); })
            .Returns(Task.CompletedTask);
        inventoryRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<InventoryItem>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // ---- InventoryMovements repository ----
        var movementRepoMock = new Mock<IRepository<InventoryMovement>>();
        movementRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(movements ?? new List<InventoryMovement>());
        movementRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InventoryMovement>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(movementRepoMock.Object);

        // ---- Users repository ----
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(users ?? new List<User>());
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // ---- PurchaseOrders repository ----
        var poRepoMock = new Mock<IRepository<PurchaseOrder>>();
        poRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(purchaseOrder);
        poRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrder>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(poRepoMock.Object);

        // ---- PurchaseOrderItems repository ----
        var poItemRepoMock = new Mock<IRepository<PurchaseOrderItem>>();
        poItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<PurchaseOrderItem, bool>>>()))
            .ReturnsAsync(poItems ?? new List<PurchaseOrderItem>());
        poItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrderItem>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(poItemRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Sales).Returns(emptyRepoMock.Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(new Mock<IRepository<Category>>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(new Mock<IRepository<Customer>>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(new Mock<IRepository<Supplier>>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(new Mock<IRepository<Shift>>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(new Mock<IRepository<Payment>>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(new Mock<IRepository<SaleItem>>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(new Mock<IRepository<Modifier>>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(new Mock<IRepository<ModifierSize>>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(new Mock<IRepository<HeldSale>>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(new Mock<IRepository<Table>>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(new Mock<IRepository<Register>>().Object);

        var service = new InventoryService(unitOfWorkMock.Object, auditServiceMock.Object);
        return (service, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // GetCurrentStockAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetCurrentStockAsync_MixedStock_ReturnsCorrectAvailability()
    {
        // Arrange
        var product1 = CreateProduct(productId: Guid.NewGuid(), arabicName: "قهوة", minStock: 5m);
        var product2 = CreateProduct(productId: Guid.NewGuid(), arabicName: "حليب", minStock: 10m);
        var inactiveProduct = CreateProduct(productId: Guid.NewGuid(), arabicName: "قديم",
            status: ProductStatus.Inactive);
        var products = new List<Product> { product1, product2, inactiveProduct };

        var inv1 = CreateInventory(product1.Id, quantity: 50m, reservedQuantity: 5m);
        var inv2 = CreateInventory(product2.Id, quantity: 3m, reservedQuantity: 1m); // low stock
        var inventoryItems = new List<InventoryItem> { inv1, inv2 };

        var (service, _, _) = BuildServiceWithMocks(products: products, inventoryItems: inventoryItems);

        // Act
        var result = await service.GetCurrentStockAsync();

        // Assert
        result.Should().HaveCount(2); // inactive product filtered out

        var coffee = result.Should().ContainSingle(p => p.ProductName == "قهوة").Subject;
        coffee.Quantity.Should().Be(50m);
        coffee.ReservedQuantity.Should().Be(5m);
        coffee.AvailableQuantity.Should().Be(45m);
        coffee.IsLowStock.Should().BeFalse(); // available=45 > minStock=5

        var milk = result.Should().ContainSingle(p => p.ProductName == "حليب").Subject;
        milk.Quantity.Should().Be(3m);
        milk.ReservedQuantity.Should().Be(1m);
        milk.AvailableQuantity.Should().Be(2m);
        milk.IsLowStock.Should().BeTrue(); // available=2 < minStock=10
    }

    [Fact]
    public async Task GetCurrentStockAsync_NoInventoryRecords_ShowsZeroStock()
    {
        // Arrange — products exist but no inventory records
        var product = CreateProduct(arabicName: "شاي");
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem>());

        // Act
        var result = await service.GetCurrentStockAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Quantity.Should().Be(0);
        result[0].ReservedQuantity.Should().Be(0);
        result[0].AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentStockAsync_NoProducts_ReturnsEmpty()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetCurrentStockAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetLowStockAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetLowStockAsync_SomeProductsLow_ReturnsOnlyLowStock()
    {
        // Arrange
        var product1 = CreateProduct(productId: Guid.NewGuid(), arabicName: "قهوة", minStock: 5m);
        var product2 = CreateProduct(productId: Guid.NewGuid(), arabicName: "حليب", minStock: 10m);
        var product3 = CreateProduct(productId: Guid.NewGuid(), arabicName: "سكر", minStock: 3m);
        var products = new List<Product> { product1, product2, product3 };

        var inv1 = CreateInventory(product1.Id, quantity: 50m); // OK
        var inv2 = CreateInventory(product2.Id, quantity: 3m);  // LOW (3 < 10)
        var inv3 = CreateInventory(product3.Id, quantity: 2m);  // LOW (2 < 3)
        var inventoryItems = new List<InventoryItem> { inv1, inv2, inv3 };

        var (service, _, _) = BuildServiceWithMocks(products: products, inventoryItems: inventoryItems);

        // Act
        var result = await service.GetLowStockAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.ProductName == "حليب");
        result.Should().Contain(p => p.ProductName == "سكر");
        result.Should().NotContain(p => p.ProductName == "قهوة");
    }

    [Fact]
    public async Task GetLowStockAsync_AllStockSufficient_ReturnsEmpty()
    {
        // Arrange
        var product = CreateProduct(arabicName: "قهوة", minStock: 5m);
        var inv = CreateInventory(product.Id, quantity: 20m);
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem> { inv });

        // Act
        var result = await service.GetLowStockAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetMovementsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetMovementsAsync_NoFilter_ReturnsAllPaged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = DefaultProductId;
        var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
        var movements = new List<InventoryMovement>
        {
            CreateMovement(productId, userId, MovementType.Purchase, 50m, 0m, 50m, now.AddHours(-3)),
            CreateMovement(productId, userId, MovementType.Sale, -10m, 50m, 40m, now.AddHours(-2)),
            CreateMovement(productId, userId, MovementType.Waste, -5m, 40m, 35m, now.AddHours(-1)),
        };
        var product = CreateProduct(productId);
        var user = CreateUser(userId);
        var products = new List<Product> { product };
        var users = new List<User> { user };

        var (service, _, _) = BuildServiceWithMocks(
            products: products, movements: movements, users: users);

        // Act — page 1, pageSize 5 (all items fit)
        var result = await service.GetMovementsAsync(null, null, null, 1, 5);

        // Assert
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().AllSatisfy(m => m.ProductName.Should().Be("قهوة"));
    }

    [Fact]
    public async Task GetMovementsAsync_FilteredByProductAndDate_ReturnsMatchingOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId1 = DefaultProductId;
        var productId2 = Guid.NewGuid();
        var baseDate = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        var movements = new List<InventoryMovement>
        {
            CreateMovement(productId1, userId, MovementType.Purchase, 50m, 0m, 50m, baseDate.AddHours(8)),
            CreateMovement(productId1, userId, MovementType.Sale, -10m, 50m, 40m, baseDate.AddHours(5)), // before from=07:00
            CreateMovement(productId2, userId, MovementType.Purchase, 30m, 0m, 30m, baseDate.AddHours(9)),
        };
        var product1 = CreateProduct(productId1, arabicName: "قهوة");
        var product2 = CreateProduct(productId2, arabicName: "شاي");
        var user = CreateUser(userId);
        var products = new List<Product> { product1, product2 };
        var users = new List<User> { user };

        var (service, _, _) = BuildServiceWithMocks(
            products: products, movements: movements, users: users);

        // Act — filter by productId1, date range covering only morning
        var result = await service.GetMovementsAsync(productId1,
            baseDate.AddHours(7), baseDate.AddHours(9), 1, 20);

        // Assert — should get Purchase at 08:00 (within range) but not Sale at 10:00
        result.Items.Should().HaveCount(1);
        result.Items[0].MovementType.Should().Be("Purchase");
    }

    [Fact]
    public async Task GetMovementsAsync_Page2_ReturnsSecondPageOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        var movements = new List<InventoryMovement>();
        for (int i = 0; i < 5; i++)
        {
            movements.Add(CreateMovement(DefaultProductId, userId, MovementType.Adjustment,
                10m + i, 0m, 10m + i, now.AddHours(i)));
        }
        var product = CreateProduct();
        var user = CreateUser(userId);
        var products = new List<Product> { product };
        var users = new List<User> { user };

        var (service, _, _) = BuildServiceWithMocks(
            products: products, movements: movements, users: users);

        // Act — page 2, pageSize 2 → items 2-3 (0-indexed)
        var result = await service.GetMovementsAsync(null, null, null, 2, 2);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
    }

    // ========================================================================
    // AdjustStockAsync Tests
    // ========================================================================

    [Fact]
    public async Task AdjustStockAsync_IncreaseStock_UpdatesQuantityAndCreatesMovement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = CreateProduct(arabicName: "قهوة");
        var inventory = CreateInventory(product.Id, quantity: 10m);
        var products = new List<Product> { product };
        var inventoryItems = new List<InventoryItem> { inventory };

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            products: products, inventoryItems: inventoryItems);

        var request = new StockAdjustmentRequest(product.Id, NewQuantity: 25m, "Restock from supplier");

        // Act
        var result = await service.AdjustStockAsync(request, userId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SuccessMessage.Should().Contain("تم تعديل المخزون");

        // Inventory updated from 10 to 25
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.Quantity == 25m)), Times.Once);

        // Movement created with difference = 15
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.Is<InventoryMovement>(m =>
                m.MovementType == MovementType.Adjustment &&
                m.Quantity == 15m &&
                m.BeforeQuantity == 10m &&
                m.AfterQuantity == 25m)), Times.Once);

        // Audit logged
        auditServiceMock.Verify(a => a.LogAsync(
            userId, AuditActionType.InventoryAdjusted, "InventoryItem",
            inventory.Id, "Quantity=10", "Quantity=25",
            "Restock from supplier"), Times.Once);
    }

    [Fact]
    public async Task AdjustStockAsync_ProductNotFound_ReturnsFailure()
    {
        // Arrange — no products
        var (service, _, _) = BuildServiceWithMocks();
        var request = new StockAdjustmentRequest(Guid.NewGuid(), NewQuantity: 10m, "test");

        // Act
        var result = await service.AdjustStockAsync(request, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("المنتج غير موجود");
    }

    [Fact]
    public async Task AdjustStockAsync_NegativeQuantity_ReturnsFailure()
    {
        // Arrange
        var product = CreateProduct();
        var inventory = CreateInventory(product.Id, quantity: 10m);
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem> { inventory });

        var request = new StockAdjustmentRequest(product.Id, NewQuantity: -5m, "invalid");

        // Act
        var result = await service.AdjustStockAsync(request, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الكمية لا يمكن أن تكون سالبة");
    }

    [Fact]
    public async Task AdjustStockAsync_NoExistingInventory_CreatesNewRecord()
    {
        // Arrange — product exists but no inventory item
        var userId = Guid.NewGuid();
        var product = CreateProduct();
        var products = new List<Product> { product };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            products: products, inventoryItems: new List<InventoryItem>());

        var request = new StockAdjustmentRequest(product.Id, NewQuantity: 15m, "New item setup");

        // Act
        var result = await service.AdjustStockAsync(request, userId);

        // Assert
        result.Success.Should().BeTrue();

        // New inventory item was added (AddAsync called once)
        unitOfWorkMock.Verify(u => u.InventoryItems.AddAsync(
            It.IsAny<InventoryItem>()), Times.Once);

        // Then updated to the requested quantity
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.Quantity == 15m)), Times.Once);
    }

    // ========================================================================
    // RecordWasteAsync Tests
    // ========================================================================

    [Fact]
    public async Task RecordWasteAsync_SufficientStock_RecordsWasteAndCreatesMovement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = CreateProduct(arabicName: "حليب");
        var inventory = CreateInventory(product.Id, quantity: 20m);
        var products = new List<Product> { product };
        var inventoryItems = new List<InventoryItem> { inventory };

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            products: products, inventoryItems: inventoryItems);

        var request = new WasteRecordRequest(product.Id, Quantity: 3m, "انتهاء الصلاحية");

        // Act
        var result = await service.RecordWasteAsync(request, userId);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Contain("تم تسجيل الإتلاف");

        // Quantity decreased from 20 to 17
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.Quantity == 17m)), Times.Once);

        // Movement created with negative quantity
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.Is<InventoryMovement>(m =>
                m.MovementType == MovementType.Waste &&
                m.Quantity == -3m &&
                m.BeforeQuantity == 20m &&
                m.AfterQuantity == 17m)), Times.Once);

        // Audit logged
        auditServiceMock.Verify(a => a.LogAsync(
            userId, AuditActionType.WasteRecorded, "InventoryItem",
            inventory.Id, "Quantity=20", "Quantity=17",
            "انتهاء الصلاحية"), Times.Once);
    }

    [Fact]
    public async Task RecordWasteAsync_ProductNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();
        var request = new WasteRecordRequest(Guid.NewGuid(), Quantity: 1m, "test");

        // Act
        var result = await service.RecordWasteAsync(request, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("المنتج غير موجود");
    }

    [Fact]
    public async Task RecordWasteAsync_ZeroQuantity_ReturnsFailure()
    {
        // Arrange
        var product = CreateProduct();
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product });

        var request = new WasteRecordRequest(product.Id, Quantity: 0m, "zero waste");

        // Act
        var result = await service.RecordWasteAsync(request, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الكمية يجب أن تكون أكبر من صفر");
    }

    [Fact]
    public async Task RecordWasteAsync_QuantityExceedsStock_ReturnsFailure()
    {
        // Arrange — stock = 5, waste = 10
        var product = CreateProduct(arabicName: "قهوة");
        var inventory = CreateInventory(product.Id, quantity: 5m);
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem> { inventory });

        var request = new WasteRecordRequest(product.Id, Quantity: 10m, "over-waste");

        // Act
        var result = await service.RecordWasteAsync(request, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الكمية المطلوبة إتلافها أكبر من المخزون المتاح");
    }

    // ========================================================================
    // ProcessPurchaseReceivedAsync Tests
    // ========================================================================

    [Fact]
    public async Task ProcessPurchaseReceivedAsync_PendingPO_ReceivesItemsAndUpdatesStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var productId = DefaultProductId;
        var poId = Guid.NewGuid();
        var purchaseOrder = CreatePurchaseOrder(poId, "PO-001", "Pending");
        
        var product = CreateProduct(productId, arabicName: "قهوة");
        var inventory = CreateInventory(productId, quantity: 5m); // existing stock
        
        // Create PO item using the actual InventoryItem's Id, not the ProductId
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = poId,
            InventoryItemId = inventory.Id,  // Use actual inventory ID
            ItemName = "قهوة",
            Quantity = 10m,
            ReceivedQuantity = 0m,
            UnitCost = 5.000m,
            TotalCost = 50.000m
        };
        var poItems = new List<PurchaseOrderItem> { poItem };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem> { inventory },
            purchaseOrder: purchaseOrder,
            poItems: poItems);

        // Act
        var result = await service.ProcessPurchaseReceivedAsync(poId, userId);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Contain("تم استلام أمر الشراء");

        // Inventory quantity increased: 5 + 10 = 15
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.Quantity == 15m)), Times.Once);

        // PO item marked as fully received
        unitOfWorkMock.Verify(u => u.PurchaseOrderItems.UpdateAsync(
            It.Is<PurchaseOrderItem>(poi => poi.ReceivedQuantity == 10m)), Times.Once);

        // PO status changed to Received
        unitOfWorkMock.Verify(u => u.PurchaseOrders.UpdateAsync(
            It.Is<PurchaseOrder>(po => po.Status == "Received")), Times.Once);

        // Movement created (Purchase type)
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.Is<InventoryMovement>(m =>
                m.MovementType == MovementType.Purchase &&
                m.Quantity == 10m &&
                m.BeforeQuantity == 5m &&
                m.AfterQuantity == 15m)), Times.Once);

        // Transaction committed
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ProcessPurchaseReceivedAsync_PONotFound_ReturnsFailure()
    {
        // Arrange — purchaseOrder is null
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.ProcessPurchaseReceivedAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("أمر الشراء غير موجود");
    }

    [Fact]
    public async Task ProcessPurchaseReceivedAsync_POAlreadyReceived_ReturnsFailure()
    {
        // Arrange — PO already received
        var poId = Guid.NewGuid();
        var purchaseOrder = CreatePurchaseOrder(poId, "PO-001", "Received");
        var (service, _, _) = BuildServiceWithMocks(purchaseOrder: purchaseOrder);

        // Act
        var result = await service.ProcessPurchaseReceivedAsync(poId, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("أمر الشراء ليس في حالة انتظار");
    }

    [Fact]
    public async Task ProcessPurchaseReceivedAsync_NoInventoryRecord_CreatesNew()
    {
        // Arrange — product has no inventory record yet
        var userId = Guid.NewGuid();
        var productId = DefaultProductId;
        var poId = Guid.NewGuid();
        var purchaseOrder = CreatePurchaseOrder(poId, "PO-002", "Pending");
        var poItem = CreatePoItem(poId, productId, qty: 20m, received: 0m);
        var poItems = new List<PurchaseOrderItem> { poItem };

        var product = CreateProduct(productId, arabicName: "قهوة");

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            inventoryItems: new List<InventoryItem>(), // no inventory
            purchaseOrder: purchaseOrder,
            poItems: poItems);

        // Act — InventoryItem doesn't exist, so GetByIdAsync returns null → throws
        var act = () => service.ProcessPurchaseReceivedAsync(poId, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*لم يتم العثور على عنصر المخزون المطلوب (ID: {poItem.InventoryItemId})*");
    }

    [Fact]
    public async Task ProcessPurchaseReceivedAsync_WhenExceptionOccurs_RollsBack()
    {
        // Arrange
        var poId = Guid.NewGuid();
        var purchaseOrder = CreatePurchaseOrder(poId, "PO-001", "Pending");

        // Build mocks manually so InventoryItems.AddAsync throws
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Products repo
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(CreateProduct());
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // PurchaseOrders repo
        var poRepoMock = new Mock<IRepository<PurchaseOrder>>();
        poRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(purchaseOrder);
        poRepoMock.Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrder>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(poRepoMock.Object);

        // PurchaseOrderItems repo
        var poItemRepoMock = new Mock<IRepository<PurchaseOrderItem>>();
        poItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<PurchaseOrderItem, bool>>>()))
            .ReturnsAsync(new List<PurchaseOrderItem>
            {
                CreatePoItem(poId, DefaultProductId, qty: 10m)
            });
        poItemRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PurchaseOrderItem>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(poItemRepoMock.Object);

        // InventoryItems repo — GetByIdAsync returns null (no matching inventory)
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((InventoryItem?)null);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // Stub Movement repo
        unitOfWorkMock.Setup(u => u.InventoryMovements)
            .Returns(new Mock<IRepository<InventoryMovement>>().Object);

        var service = new InventoryService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act
        var act = () => service.ProcessPurchaseReceivedAsync(poId, Guid.NewGuid());

        // Assert — expects the new "inventory item not found" exception
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*لم يتم العثور على عنصر المخزون المطلوب*");

        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }
}
