#nullable enable

using System.Linq.Expressions;
using System.Text.Json;
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
/// Unit tests for SaleService.AddItemAsync and SaleService.ProcessPaymentAsync.
///
/// AddItemAsync scenarios:
///   1. Happy path — basic item added with correct line total calculation
///   2. Sale not found   3. Sale not active   4. Product not found
///   5. Product inactive  6. Insufficient stock  7. With modifiers
///   8. Inventory reservation
///
/// ProcessPaymentAsync scenarios:
///   1. Happy path cash with change   2. Sale not found   3. Sale not active
///   4. Insufficient amount   5. Zero payment   6. Card payment with reference
///   7. Inventory deduction + movement  8. Shift update  9. Exact payment (no change)
///  10. Transaction rollback on exception
/// </summary>
public class SaleServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultProductId = Guid.NewGuid();
    private const decimal DefaultPrice = 10.000m;
    private const decimal DefaultTaxRate = 0.16m; // 16%
    private const decimal DefaultCost = 5.000m;

    /// <summary>
    /// Creates an active sale ready for items/payment.
    /// </summary>
    private static Sale CreateActiveSale(Guid saleId, Guid? shiftId = null, Guid? userId = null)
    {
        return new Sale
        {
            Id = saleId,
            InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-0001",
            ShiftId = shiftId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Status = SaleStatus.Active,
            SubTotal = 0,
            TaxAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 0,
            IsPaid = false
        };
    }

    /// <summary>
    /// Creates a sale with a non-Active status (Completed, Held, etc.).
    /// </summary>
    private static Sale CreateNonActiveSale(Guid saleId, SaleStatus status)
    {
        var sale = CreateActiveSale(saleId);
        sale.Status = status;
        return sale;
    }

    /// <summary>
    /// Creates a test product with the given properties.
    /// </summary>
    private static Product CreateTestProduct(Guid? productId = null, decimal price = DefaultPrice,
        decimal taxRate = DefaultTaxRate, ProductStatus status = ProductStatus.Active)
    {
        return new Product
        {
            Id = productId ?? DefaultProductId,
            ArabicName = "منتج اختبار",
            Name = "Test Product",
            Price = price,
            Cost = DefaultCost,
            TaxRate = taxRate,
            Status = status,
            Unit = "piece"
        };
    }

    /// <summary>
    /// Creates an inventory record with the specified quantities.
    /// </summary>
    private static InventoryItem CreateTestInventory(Guid productId, decimal quantity = 10m,
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

    /// <summary>
    /// Creates a test shift in Open status with zero totals.
    /// </summary>
    private static Shift CreateTestShift(Guid shiftId)
    {
        return new Shift
        {
            Id = shiftId,
            ShiftNumber = 1,
            Status = ShiftStatus.Open,
            TotalSales = 0,
            TotalReturns = 0,
            OpenedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test modifier.
    /// </summary>
    private static Modifier CreateTestModifier(Guid modifierId, string name = "Extra Cheese",
        decimal price = 2.000m)
    {
        return new Modifier
        {
            Id = modifierId,
            Name = name,
            Price = price,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a test modifier size (e.g., Large size adjustment).
    /// </summary>
    private static ModifierSize CreateTestModifierSize(Guid modifierId, decimal priceAdjustment = 1.000m)
    {
        return new ModifierSize
        {
            Id = Guid.NewGuid(),
            ModifierId = modifierId,
            Name = "Large",
            PriceAdjustment = priceAdjustment
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Creates an empty Mock for IRepository{T} that returns empty lists from FindAsync
    /// (prevents NRE when a repo is accessed but not expected to return data).
    /// </summary>
    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a SaleService with fully mocked IUnitOfWork and IAuditService.
    /// Each repository returns the provided data; null means empty/no results.
    ///
    /// For AddItemAsync tests, the product and inventory must be provided.
    /// For ProcessPaymentAsync tests, the sale must have items already added
    /// (via sale.AddItem) and an inventory item must be provided.
    /// </summary>
    private (SaleService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            Sale? sale,
            Product? product,
            InventoryItem? inventory = null,
            Shift? shift = null,
            List<Modifier>? modifiers = null,
            List<ModifierSize>? modifierSizes = null,
            List<SaleItem>? saleItems = null,
            List<Payment>? payments = null,
            List<HeldSale>? heldSales = null,
            List<Sale>? existingSales = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        // Audit service — fire-and-forget, always succeeds
        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var saleId = sale?.Id ?? Guid.Empty;
        var productId = product?.Id ?? Guid.Empty;

        // ---- Transactions ----
        unitOfWorkMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetByIdAsync(saleId))
            .ReturnsAsync(sale);
        saleRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Sale>())).Returns(Task.CompletedTask);
        saleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Sale, bool>>>()))
            .ReturnsAsync((Expression<Func<Sale, bool>> predicate) =>
                (existingSales ?? new List<Sale>()).AsQueryable().Where(predicate).ToList());
        saleRepoMock.Setup(r => r.AddAsync(It.IsAny<Sale>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Products repository ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- InventoryItems repository ----
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>()))
            .ReturnsAsync(inventory != null ? new List<InventoryItem> { inventory } : new List<InventoryItem>());
        inventoryRepoMock.Setup(r => r.UpdateAsync(It.IsAny<InventoryItem>())).Returns(Task.CompletedTask);
        inventoryRepoMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // ---- SaleItems repository ----
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync((Expression<Func<SaleItem, bool>> predicate) =>
                (saleItems ?? new List<SaleItem>()).AsQueryable().Where(predicate).ToList());
        saleItemRepoMock.Setup(r => r.AddAsync(It.IsAny<SaleItem>())).Returns(Task.CompletedTask);
        saleItemRepoMock.Setup(r => r.UpdateAsync(It.IsAny<SaleItem>())).Returns(Task.CompletedTask);
        saleItemRepoMock.Setup(r => r.DeleteAsync(It.IsAny<SaleItem>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // ---- InventoryBatches repository ----
        var batchRepoMock = new Mock<IRepository<InventoryBatch>>();
        batchRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryBatch, bool>>>()))
            .ReturnsAsync(new List<InventoryBatch>());
        batchRepoMock.Setup(r => r.UpdateAsync(It.IsAny<InventoryBatch>())).Returns(Task.CompletedTask);
        batchRepoMock.Setup(r => r.AddAsync(It.IsAny<InventoryBatch>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryBatches).Returns(batchRepoMock.Object);

        // ---- InventoryMovements repository ----
        var movementRepoMock = new Mock<IRepository<InventoryMovement>>();
        movementRepoMock.Setup(r => r.AddAsync(It.IsAny<InventoryMovement>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(movementRepoMock.Object);

        // ---- Modifiers repository ----
        var modRepoMock = new Mock<IRepository<Modifier>>();
        modRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
                modifiers?.FirstOrDefault(m => m.Id == id));
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(modRepoMock.Object);

        // ---- ModifierSizes repository ----
        var modSizeRepoMock = new Mock<IRepository<ModifierSize>>();
        modSizeRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
                modifierSizes?.FirstOrDefault(s => s.Id == id));
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(modSizeRepoMock.Object);

        // ---- Payments repository ----
        var paymentRepoMock = new Mock<IRepository<Payment>>();
        paymentRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>()))
            .ReturnsAsync(payments ?? new List<Payment>());
        paymentRepoMock.Setup(r => r.AddAsync(It.IsAny<Payment>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Payments).Returns(paymentRepoMock.Object);

        // ---- Shifts repository ----
        var shiftRepoMock = new Mock<IRepository<Shift>>();
        shiftRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(shift);
        shiftRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Shift>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(shiftRepoMock.Object);

        // ---- HeldSales repository ----
        var heldSaleRepoMock = new Mock<IRepository<HeldSale>>();
        heldSaleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<HeldSale, bool>>>()))
            .ReturnsAsync(heldSales ?? new List<HeldSale>());
        heldSaleRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => heldSales?.FirstOrDefault(h => h.Id == id));
        heldSaleRepoMock
            .Setup(r => r.AddAsync(It.IsAny<HeldSale>()))
            .Callback<HeldSale>(hs => { if (hs.Id == Guid.Empty) hs.Id = Guid.NewGuid(); })
            .Returns(Task.CompletedTask);
        heldSaleRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<HeldSale>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(heldSaleRepoMock.Object);

        // ---- Stub remaining repos to prevent NullReferenceException ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);
        unitOfWorkMock.Setup(u => u.SalePromotions).Returns(CreateEmptyRepoMock<SalePromotion>().Object);

        var service = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        return (service, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // AddItemAsync Tests
    // ========================================================================

    [Fact]
    public async Task AddItemAsync_HappyPath_AddsItemWithCorrectTotals()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var product = CreateTestProduct(price: 10.000m, taxRate: 0.16m);
        var inventory = CreateTestInventory(product.Id, quantity: 10m);
        var sale = CreateActiveSale(saleId);

        var request = new AddItemRequest(product.Id, Quantity: 2m, Notes: null, Modifiers: null);

        var (service, _, _) = BuildServiceWithMocks(sale, product, inventory);

        // Act
        await service.AddItemAsync(saleId, request);

        // Assert
        sale.SaleItems.Should().HaveCount(1);
        var item = sale.SaleItems.First();

        item.ProductId.Should().Be(product.Id);
        item.Quantity.Should().Be(2m);
        item.UnitPrice.Should().Be(10.000m);
        item.Discount.Should().Be(0);
        item.TaxRate.Should().Be(0.16m);

        // Line total: Round(Round(10 * 2) * (1 + 0.16)) = Round(20 * 1.16) = 23.200
        item.TaxAmount.Should().Be(3.200m);
        item.LineTotal.Should().Be(23.200m);

        // Sale totals should be recalculated
        sale.SubTotal.Should().Be(20.000m);
        sale.TaxAmount.Should().Be(3.200m);
        sale.TotalAmount.Should().Be(23.200m);
    }

    [Fact]
    public async Task AddItemAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale returns null
        var saleId = Guid.NewGuid();
        var product = CreateTestProduct();
        var inventory = CreateTestInventory(product.Id);
        var request = new AddItemRequest(product.Id, Quantity: 1m, Notes: null, Modifiers: null);

        var (service, _, _) = BuildServiceWithMocks(sale: null, product, inventory);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    [Fact]
    public async Task AddItemAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();
        var inventory = CreateTestInventory(product.Id);
        var request = new AddItemRequest(product.Id, Quantity: 1m, Notes: null, Modifiers: null);

        var (service, _, _) = BuildServiceWithMocks(sale, product, inventory);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("لا يمكن إضافة عناصر لبيع غير نشط");
    }

    [Fact]
    public async Task AddItemAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var request = new AddItemRequest(Guid.NewGuid(), Quantity: 1m, Notes: null, Modifiers: null);

        // Product returns null
        var (service, _, _) = BuildServiceWithMocks(sale, product: null);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المنتج غير موجود");
    }

    [Fact]
    public async Task AddItemAsync_ProductInactive_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct(status: ProductStatus.Inactive);
        var inventory = CreateTestInventory(product.Id);
        var request = new AddItemRequest(product.Id, Quantity: 1m, Notes: null, Modifiers: null);

        var (service, _, _) = BuildServiceWithMocks(sale, product, inventory);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المنتج غير نشط");
    }

    [Fact]
    public async Task AddItemAsync_InsufficientStock_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();
        // Inventory has 5 items, but we request 10
        var inventory = CreateTestInventory(product.Id, quantity: 5m);
        var request = new AddItemRequest(product.Id, Quantity: 10m, Notes: null, Modifiers: null);

        var (service, _, _) = BuildServiceWithMocks(sale, product, inventory);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert — AvailableQuantity = 5 - 0 = 5, request.Quantity = 10
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الكمية المتاحة غير كافية. المتاح: 5");
    }

    [Fact]
    public async Task AddItemAsync_WithModifiers_IncludesModifierExtraInLineTotal()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var product = CreateTestProduct(price: 10.000m, taxRate: 0.16m);
        var inventory = CreateTestInventory(product.Id, quantity: 10m);
        var sale = CreateActiveSale(saleId);

        var modifierId = Guid.NewGuid();
        var modifier = CreateTestModifier(modifierId, "Extra Cheese", price: 2.000m);
        var modifiers = new List<Modifier> { modifier };

        var request = new AddItemRequest(product.Id, Quantity: 2m, Notes: null,
            Modifiers: new List<ModifierSelectionDto>
            {
                new(modifierId, ModifierSizeId: null, Quantity: 1)
            });

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, inventory, modifiers: modifiers);

        // Act
        await service.AddItemAsync(saleId, request);

        // Assert
        sale.SaleItems.Should().HaveCount(1);
        var item = sale.SaleItems.First();

        // Modifiers should be attached to the sale item
        item.Modifiers.Should().HaveCount(1);
        item.Modifiers.First().ModifierName.Should().Be("Extra Cheese");
        item.Modifiers.First().AdditionalPrice.Should().Be(2.000m);

        // ModifierSummary should be populated
        item.ModifierSummary.Should().Be("Extra Cheese");

        // RecalculateSaleTotals now includes modifiers: 2*10=20 + 2 = 22 pre-tax, tax=3.520, total=25.520
        item.TaxAmount.Should().Be(3.520m);
        item.LineTotal.Should().Be(25.520m);

        // Sale totals
        sale.SubTotal.Should().Be(22.000m);
        sale.TaxAmount.Should().Be(3.520m);
        sale.TotalAmount.Should().Be(25.520m);
    }

    [Fact]
    public async Task AddItemAsync_WithModifierAndSize_IncludesSizePriceAdjustment()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var product = CreateTestProduct(price: 10.000m);
        var inventory = CreateTestInventory(product.Id, quantity: 10m);
        var sale = CreateActiveSale(saleId);

        var modifierId = Guid.NewGuid();
        var modifier = CreateTestModifier(modifierId, "Extra Cheese", price: 2.000m);
        var modSize = CreateTestModifierSize(modifierId, priceAdjustment: 1.500m);
        var modifiers = new List<Modifier> { modifier };
        var modifierSizes = new List<ModifierSize> { modSize };

        var request = new AddItemRequest(product.Id, Quantity: 1m, Notes: null,
            Modifiers: new List<ModifierSelectionDto>
            {
                new(modifierId, ModifierSizeId: modSize.Id, Quantity: 1)
            });

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, inventory, modifiers: modifiers, modifierSizes: modifierSizes);

        // Act
        await service.AddItemAsync(saleId, request);

        // Assert — modifier price + size adjustment
        var item = sale.SaleItems.First();
        item.Modifiers.Should().HaveCount(1);

        // Price: 2.000 + 1.500 = 3.500
        item.Modifiers.First().AdditionalPrice.Should().Be(3.500m);
    }

    [Fact]
    public async Task AddItemAsync_ReservesInventory()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var product = CreateTestProduct();
        var inventory = CreateTestInventory(product.Id, quantity: 10m, reservedQuantity: 0m);
        var sale = CreateActiveSale(saleId);

        var request = new AddItemRequest(product.Id, Quantity: 3m, Notes: null, Modifiers: null);

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product, inventory);

        // Act
        await service.AddItemAsync(saleId, request);

        // Assert — inventory reservation should have been called
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.ReservedQuantity == 3m)), Times.Once);

        // Transaction should be committed\r\n        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    // ========================================================================
    // ProcessPaymentAsync Tests
    // ========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_CashPayment_ReturnsSuccessWithChange()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        var shift = CreateTestShift(shiftId);

        // Add an item to the sale to set up totals
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 2m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 2m);
        var product = CreateTestProduct();

        var saleItems = new List<SaleItem> { item };
        var request = new PaymentRequest(saleId, Amount: 30.000m, "Cash", ReferenceNumber: null);

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ChangeAmount.Should().Be(6.800m); // 30.000 - 23.200
    }

    [Fact]
    public async Task ProcessPaymentAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var request = new PaymentRequest(saleId, Amount: 10m, "Cash", null);

        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.ProcessPaymentAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    [Fact]
    public async Task ProcessPaymentAsync_SaleNotActive_ReturnsFailure()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();
        var request = new PaymentRequest(saleId, Amount: 10m, "Cash", null);

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("البيع غير نشط");
        result.ChangeAmount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPaymentAsync_InsufficientAmount_ReturnsFailure()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m; // Sale total is 23.200, paying only 10

        var product = CreateTestProduct();
        var request = new PaymentRequest(saleId, Amount: 10.000m, "Cash", null);

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert — Validation: amountPaid (10) < amountDue (23.200)
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("أقل من");
        result.ChangeAmount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ZeroPayment_ReturnsFailure()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var product = CreateTestProduct();
        var request = new PaymentRequest(saleId, Amount: 0m, "Cash", null);

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("أكبر من صفر");
        result.ChangeAmount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardPayment_SetsPaymentMethodAndReference()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 1m,
            UnitPrice = 50.000m,
            TaxRate = 0.16m,
            TaxAmount = 8.000m,
            LineTotal = 58.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 1m);
        var product = CreateTestProduct(price: 50.000m);
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { item };

        var request = new PaymentRequest(saleId, Amount: 58.000m, "Card", "REF-ABC-123");

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ChangeAmount.Should().Be(0m); // exact payment

        // Verify the payment was created with correct properties
        unitOfWorkMock.Verify(u => u.Payments.AddAsync(
            It.Is<Payment>(p =>
                p.PaymentMethod == PaymentMethod.Card &&
                p.ReferenceNumber == "REF-ABC-123" &&
                p.Amount == 58.000m)), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_DeductsInventoryAndCreatesMovement()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 3m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 4.800m,
            LineTotal = 34.800m
        };
        sale.AddItem(item);
        sale.SubTotal = 30.000m;
        sale.TaxAmount = 4.800m;
        sale.TotalAmount = 34.800m;

        // Inventory: 10 in stock, 3 reserved
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 3m);
        var product = CreateTestProduct();
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { item };

        var request = new PaymentRequest(saleId, Amount: 34.800m, "Cash", null);

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Inventory quantity should be reduced from 10 to 7
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv =>
                inv.Quantity == 7m &&
                inv.ReservedQuantity == 0m)), Times.Once);

        // Inventory movement should be recorded
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.Is<InventoryMovement>(m =>
                m.MovementType == MovementType.Sale &&
                m.Quantity == -3m &&
                m.BeforeQuantity == 10m &&
                m.AfterQuantity == 7m)), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_UpdatesShiftTotalSales()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 2m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 2m);
        var product = CreateTestProduct();
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { item };

        var request = new PaymentRequest(saleId, Amount: 23.200m, "Cash", null);

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Shift TotalSales should be updated from 0 to 23.200
        unitOfWorkMock.Verify(u => u.Shifts.UpdateAsync(
            It.Is<Shift>(s =>
                s.TotalSales == 23.200m)), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ExactPayment_NoChange()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 1m,
            UnitPrice = 15.500m,
            TaxRate = 0.16m,
            TaxAmount = 2.480m,
            LineTotal = 17.980m
        };
        sale.AddItem(item);
        sale.SubTotal = 15.500m;
        sale.TaxAmount = 2.480m;
        sale.TotalAmount = 17.980m;

        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 1m);
        var product = CreateTestProduct(price: 15.500m);
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { item };

        var request = new PaymentRequest(saleId, Amount: 17.980m, "Cash", null);

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ChangeAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenExceptionOccurs_RollsBackTransaction()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 1m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 1.600m,
            LineTotal = 11.600m
        };
        sale.AddItem(item);
        sale.SubTotal = 10.000m;
        sale.TaxAmount = 1.600m;
        sale.TotalAmount = 11.600m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };

        // Build all mocks manually — InventoryItems.FindAsync will throw
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

        // Sale repo
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(sale);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // Product repo
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(product);
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // SaleItems repo — returns the item
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync(saleItems);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // InventoryItems repo — throws on FindAsync (simulates DB failure inside payment loop)
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // Stub remaining repos to prevent NRE
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
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
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        var request = new PaymentRequest(saleId, Amount: 11.600m, "Cash", null);

        // Act
        var act = () => service.ProcessPaymentAsync(request);

        // Assert — the exception propagates, and RollbackAsync is called
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB connection lost");

        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    // ========================================================================
    // HoldSaleAsync Tests
    // ========================================================================

    /// <summary>
    /// Helper to verify a HeldSale's serialized JSON contains expected item data.
    /// Used inside It.Is{} predicates which cannot have statement-body lambdas.
    /// </summary>
    private static bool ContainsItemInSerializedData(HeldSale hs, string expectedProductName,
        decimal expectedQty, decimal expectedUnitPrice, decimal expectedLineTotal)
    {
        var doc = JsonDocument.Parse(hs.SerializedData);
        var itemsArray = doc.RootElement.GetProperty("Items").EnumerateArray().ToList();
        if (itemsArray.Count != 1) return false;
        var serializedItem = itemsArray[0];
        return serializedItem.GetProperty("ProductName").GetString() == expectedProductName &&
               serializedItem.GetProperty("Quantity").GetDecimal() == expectedQty &&
               serializedItem.GetProperty("UnitPrice").GetDecimal() == expectedUnitPrice &&
               serializedItem.GetProperty("LineTotal").GetDecimal() == expectedLineTotal &&
               serializedItem.GetProperty("Notes").ValueKind == System.Text.Json.JsonValueKind.Null;
    }


    [Fact]
    public async Task HoldSaleAsync_HappyPath_SerializesDataAndReturnsHeldId()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var heldSaleId = await service.HoldSaleAsync(saleId, "Customer request");

        // Assert — returned ID is non-empty
        heldSaleId.Should().NotBe(Guid.Empty);

        // Sale status changed to Held
        sale.Status.Should().Be(SaleStatus.Held);
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Held)), Times.Once);

        // HeldSale was added with correct properties
        unitOfWorkMock.Verify(u => u.HeldSales.AddAsync(
            It.Is<HeldSale>(hs =>
                hs.HoldReason == "Customer request" &&
                hs.ShiftId == shiftId &&
                hs.UserId == userId &&
                // Verify JSON serialization contains key fields
                JsonDocument.Parse(hs.SerializedData).RootElement.GetProperty("SaleId").GetGuid() == saleId &&
                JsonDocument.Parse(hs.SerializedData).RootElement.GetProperty("TotalAmount").GetDecimal() == 58.000m &&
                JsonDocument.Parse(hs.SerializedData).RootElement.GetProperty("SubTotal").GetDecimal() == 50.000m &&
                JsonDocument.Parse(hs.SerializedData).RootElement.GetProperty("TaxAmount").GetDecimal() == 8.000m
            )), Times.Once);

        // SaveChanges was called
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HoldSaleAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.HoldSaleAsync(Guid.NewGuid(), "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0628\u064A\u0639 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task HoldSaleAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange — sale is completed, not active
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.HoldSaleAsync(saleId, "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u064A\u0645\u0643\u0646 \u0641\u0642\u0637 \u0648\u0636\u0639 \u0627\u0644\u0628\u064A\u0639 \u0627\u0644\u0646\u0634\u0637 \u0641\u064A \u0627\u0644\u0627\u0646\u062A\u0638\u0627\u0631");
    }

    [Fact]
    public async Task HoldSaleAsync_WithItems_SerializesItemsInJson()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        // Add an item to the sale
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 2m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = 5.000m,
            Notes = null,
            ModifierSummary = null
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        // Act
        var heldSaleId = await service.HoldSaleAsync(saleId, "Table waiting");

        // Assert — verify items are serialized using helper (single-expression lambda)
        unitOfWorkMock.Verify(u => u.HeldSales.AddAsync(
            It.Is<HeldSale>(hs => ContainsItemInSerializedData(hs, "قهوة", 2m, 10.000m, 23.200m))
        ), Times.Once);
    }

    [Fact]
    public async Task HoldSaleAsync_WithoutReason_AllowsNullHoldReason()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var heldSaleId = await service.HoldSaleAsync(saleId, null!);

        // Assert — HeldSale was added with null HoldReason
        heldSaleId.Should().NotBe(Guid.Empty);
        unitOfWorkMock.Verify(u => u.HeldSales.AddAsync(
            It.Is<HeldSale>(hs => hs.HoldReason == null)), Times.Once);
    }

    // ========================================================================
    // RetrieveHeldSaleAsync Tests
    // ========================================================================

    [Fact]
    public async Task RetrieveHeldSaleAsync_HappyPath_RestoresSaleAndDeletesHeld()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var heldSaleId = Guid.NewGuid();
        var serializedData = JsonSerializer.Serialize(new
        {
            SaleId = saleId,
            InvoiceNumber = sale.InvoiceNumber,
            Items = new List<object>(),
            SubTotal = 50.000m,
            TaxAmount = 8.000m,
            DiscountAmount = 0m,
            TotalAmount = 58.000m
        });

        var heldSale = new HeldSale
        {
            Id = heldSaleId,
            SerializedData = serializedData,
            ShiftId = shiftId,
            UserId = userId,
            HoldReason = "Customer request",
            CreatedAt = DateTime.UtcNow
        };

        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, heldSales: new List<HeldSale> { heldSale });

        // Act
        var result = await service.RetrieveHeldSaleAsync(heldSaleId);

        // Assert — SaleSummaryDto has correct values from serialized data
        result.SaleId.Should().Be(saleId);
        result.TotalAmount.Should().Be(58.000m);
        result.InvoiceNumber.Should().StartWith("Held-");
        result.InvoiceNumber.Should().Contain("Customer request");
        result.Status.Should().Be("Active");

        // Sale status was restored to Active
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Active)), Times.Once);

        // HeldSale was deleted
        unitOfWorkMock.Verify(u => u.HeldSales.DeleteAsync(
            It.Is<HeldSale>(hs => hs.Id == heldSaleId)), Times.Once);

        // SaveChanges was called
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RetrieveHeldSaleAsync_HeldSaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — heldSales list is empty so GetByIdAsync returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.RetrieveHeldSaleAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0628\u064A\u0639 \u0627\u0644\u0645\u062D\u062A\u0641\u0638 \u0628\u0647 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task RetrieveHeldSaleAsync_SaleWasDeleted_StillDeletesHeldSale()
    {
        // Arrange — heldSale exists, but the original sale was deleted (sale = null)
        var heldSaleId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var serializedData = JsonSerializer.Serialize(new
        {
            SaleId = saleId,
            InvoiceNumber = "INV-DELETED-001",
            Items = new List<object>(),
            SubTotal = 0m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100.000m
        });

        var heldSale = new HeldSale
        {
            Id = heldSaleId,
            SerializedData = serializedData,
            ShiftId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HoldReason = "No reason",
            CreatedAt = DateTime.UtcNow
        };

        // Pass sale: null, but heldSale exists
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null, heldSales: new List<HeldSale> { heldSale });

        // Act
        var result = await service.RetrieveHeldSaleAsync(heldSaleId);

        // Assert — still returns a summary with parsed values
        result.SaleId.Should().Be(saleId);
        result.TotalAmount.Should().Be(100.000m);
        result.Status.Should().Be("Active");

        // Sale update should NOT be called (sale was null)
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(It.IsAny<Sale>()), Times.Never);

        // HeldSale should still be deleted
        unitOfWorkMock.Verify(u => u.HeldSales.DeleteAsync(
            It.Is<HeldSale>(hs => hs.Id == heldSaleId)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RetrieveHeldSaleAsync_MalformedJson_HandlesGracefully()
    {
        // Arrange — SerializedData is not valid JSON
        var heldSaleId = Guid.NewGuid();
        var heldSale = new HeldSale
        {
            Id = heldSaleId,
            SerializedData = "not a valid json string at all!!",
            ShiftId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HoldReason = "broken",
            CreatedAt = DateTime.UtcNow
        };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null, heldSales: new List<HeldSale> { heldSale });

        // Act
        var result = await service.RetrieveHeldSaleAsync(heldSaleId);

        // Assert — try/catch caught the parse error, saleId = Empty, totalAmount = 0
        result.SaleId.Should().Be(Guid.Empty);
        result.TotalAmount.Should().Be(0);
        result.InvoiceNumber.Should().Be("Held-broken");
        result.Status.Should().Be("Active");

        // HeldSale should still be deleted
        unitOfWorkMock.Verify(u => u.HeldSales.DeleteAsync(
            It.Is<HeldSale>(hs => hs.Id == heldSaleId)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    // ========================================================================
    // CancelSaleAsync Tests
    // ========================================================================

    [Fact]
    public async Task CancelSaleAsync_ActiveSaleWithItems_CancelsAndReleasesInventory()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.Status = SaleStatus.Active;

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 3m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 4.800m,
            LineTotal = 34.800m,
            Cost = 5.000m
        };
        sale.AddItem(item);

        var product = CreateTestProduct();
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 3m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            sale, product, inventory, saleItems: saleItems);

        // Act
        var result = await service.CancelSaleAsync(saleId, "Customer changed mind");

        // Assert — result indicates success
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SuccessMessage.Should().Be("\u062A\u0645 \u0625\u0644\u063A\u0627\u0621 \u0627\u0644\u0628\u064A\u0639 \u0628\u0646\u062C\u0627\u062D");

        // Sale status changed to Cancelled
        sale.Status.Should().Be(SaleStatus.Cancelled);
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Cancelled)), Times.Once);

        // Inventory reserved quantity was released (3 - 3 = 0)
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.ReservedQuantity == 0m)), Times.Once);

        // Audit was logged with CancellationProcessed
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.CancellationProcessed,
            "Sale",
            saleId,
            "Status=Active",
            "Status=Cancelled",
            "Customer changed mind"), Times.Once);

        // Transaction was committed
        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CancelSaleAsync_SaleNotFound_ReturnsFailure()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var result = await service.CancelSaleAsync(Guid.NewGuid(), "reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("\u0627\u0644\u0628\u064A\u0639 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
        result.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task CancelSaleAsync_CompletedSale_ReturnsFailure()
    {
        // Arrange — sale is already completed
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.CancelSaleAsync(saleId, "late reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("\u0644\u0627 \u064A\u0645\u0643\u0646 \u0625\u0644\u063A\u0627\u0621 \u0647\u0630\u0627 \u0627\u0644\u0628\u064A\u0639");

        // Transaction should NOT have been started (early return)
        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelSaleAsync_AlreadyCancelledSale_ReturnsFailure()
    {
        // Arrange — sale is already cancelled
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Cancelled);
        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.CancelSaleAsync(saleId, "double cancel");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("\u0644\u0627 \u064A\u0645\u0643\u0646 \u0625\u0644\u063A\u0627\u0621 \u0647\u0630\u0627 \u0627\u0644\u0628\u064A\u0639");

        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelSaleAsync_HeldSale_CancelsSuccessfully()
    {
        // Arrange — held sales are also cancellable (not Completed or Cancelled)
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Held);
        var product = CreateTestProduct();

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.CancelSaleAsync(saleId, "Table no-show");

        // Assert
        result.Success.Should().BeTrue();

        // Sale status should change from Held to Cancelled
        sale.Status.Should().Be(SaleStatus.Cancelled);
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Cancelled)), Times.Once);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenExceptionOccurs_RollsBackTransaction()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 1m,
            UnitPrice = 10.000m
        };
        sale.AddItem(item);

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };

        // Manually build mocks so InventoryItems.FindAsync throws
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

        // Sale repo
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(sale);
        saleRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Sale>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // SaleItems repo — returns the item
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync(saleItems);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // InventoryItems repo — throws to simulate DB failure inside cancellation loop
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // Stub remaining repos
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
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
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act
        var act = () => service.CancelSaleAsync(saleId, "DB error");

        // Assert — exception propagates
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB connection lost");

        // Rollback was called, commit never
        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelSaleAsync_NoSaleItems_StillCancelsSuccessfully()
    {
        // Arrange — sale with no items (empty sale)
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.CancelSaleAsync(saleId, "Empty order");

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Contain("\u0625\u0644\u063A\u0627\u0621");

        // Sale status changed to Cancelled
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Cancelled)), Times.Once);

        // Transaction was committed
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    // ========================================================================
    // ReturnItemsAsync Tests
    // ========================================================================

    [Fact]
    public async Task ReturnItemsAsync_CompletedSale_ReturnsItemRestoresInventoryAndUpdatesShift()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.Status = SaleStatus.Completed; // Must be completed for returns
        sale.InvoiceNumber = "INV-001";

        var saleItemId = Guid.NewGuid();
        var saleItem = new SaleItem
        {
            Id = saleItemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "\u0642\u0647\u0648\u0629",
            Quantity = 2m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = 5.000m
        };
        sale.AddItem(saleItem);

        var product = CreateTestProduct();
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m);
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { saleItem };

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        var returnRequest = new ReturnItemRequest(saleItemId, Quantity: 1m, "\u0645\u0646\u062A\u062C \u062A\u0627\u0644\u0641");

        // Act
        var result = await service.ReturnItemsAsync(saleId, new List<ReturnItemRequest> { returnRequest }, "Customer complaint");

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SuccessMessage.Should().Contain("10.000"); // return amount = 10 * 1 = 10.000
        result.SuccessMessage.Should().Contain("\u062A\u0645 \u0625\u0631\u062C\u0627\u0639");

        // Inventory restored: 10 + 1 = 11
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.Quantity == 11m)), Times.Once);

        // Inventory movement created
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.Is<InventoryMovement>(m =>
                m.MovementType == MovementType.Return &&
                m.Quantity == 1m &&
                m.BeforeQuantity == 10m &&
                m.AfterQuantity == 11m)), Times.Once);

        // Shift TotalReturns updated
        unitOfWorkMock.Verify(u => u.Shifts.UpdateAsync(
            It.Is<Shift>(s => s.TotalReturns == 10.000m)), Times.Once);

        // Original sale status changed to Returned
        sale.Status.Should().Be(SaleStatus.Returned);
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.Status == SaleStatus.Returned)), Times.Once);

        // Return entity added
        unitOfWorkMock.Verify(u => u.Returns.AddAsync(
            It.Is<Return>(r =>
                r.TotalAmount == 10.000m &&
                r.Status == "Processed")), Times.Once);

        // Audit logged
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.ReturnProcessed,
            "Return",
            It.IsAny<Guid?>(),
            null,
            "Amount=10.000",
            "Customer complaint"), Times.Once);

        // Transaction committed
        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ReturnItemsAsync_OriginalSaleNotFound_ReturnsFailure()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var result = await service.ReturnItemsAsync(
            Guid.NewGuid(),
            new List<ReturnItemRequest>(),
            "reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("\u0627\u0644\u0628\u064A\u0639 \u0627\u0644\u0623\u0635\u0644\u064A \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
        result.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task ReturnItemsAsync_SaleNotCompleted_ReturnsFailure()
    {
        // Arrange — sale is active, not completed
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.Status = SaleStatus.Active;
        var product = CreateTestProduct();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.ReturnItemsAsync(
            saleId,
            new List<ReturnItemRequest>(),
            "reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("\u064A\u0645\u0643\u0646 \u0641\u0642\u0637 \u0625\u0631\u062C\u0627\u0639 \u0639\u0646\u0627\u0635\u0631 \u0645\u0646 \u0628\u064A\u0639 \u0645\u0643\u062A\u0645\u0644");

        // Transaction should NOT have been started (early return)
        unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task ReturnItemsAsync_SaleItemNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale exists but saleItem not found
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.Status = SaleStatus.Completed;
        var product = CreateTestProduct();

        // No saleItems provided → FindAsync returns empty list → item not found
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        var returnRequest = new ReturnItemRequest(Guid.NewGuid(), Quantity: 1m, "defective");

        // Act
        var act = () => service.ReturnItemsAsync(
            saleId,
            new List<ReturnItemRequest> { returnRequest },
            "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0639\u0646\u0635\u0631 \u0627\u0644\u0628\u064A\u0639 * \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task ReturnItemsAsync_QuantityExceedsSoldQty_ThrowsInvalidOperationException()
    {
        // Arrange — return qty (3) > sold qty (2)
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.Status = SaleStatus.Completed;

        var saleItemId = Guid.NewGuid();
        var saleItem = new SaleItem
        {
            Id = saleItemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 2m, // Only 2 were sold
            UnitPrice = 10.000m
        };
        sale.AddItem(saleItem);

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { saleItem };
        var (service, _, _) = BuildServiceWithMocks(sale, product, saleItems: saleItems);

        var returnRequest = new ReturnItemRequest(saleItemId, Quantity: 3m, "over-return");

        // Act
        var act = () => service.ReturnItemsAsync(
            saleId,
            new List<ReturnItemRequest> { returnRequest },
            "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0643\u0645\u064A\u0629 \u0627\u0644\u0645\u0631\u062A\u062C\u0639\u0629 \u0623\u0643\u0628\u0631 \u0645\u0646 \u0627\u0644\u0643\u0645\u064A\u0629 \u0627\u0644\u0645\u0628\u0627\u0639\u0629");
    }

    [Fact]
    public async Task ReturnItemsAsync_MultipleItems_AccumulatesTotalReturnAmount()
    {
        // Arrange — return 2 items with different prices
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.Status = SaleStatus.Completed;
        sale.InvoiceNumber = "INV-002";

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        var saleItem1 = new SaleItem
        {
            Id = item1Id,
            SaleId = saleId,
            ProductId = productId1,
            ProductName = "Coffee",
            Quantity = 2m,
            UnitPrice = 10.000m
        };
        var saleItem2 = new SaleItem
        {
            Id = item2Id,
            SaleId = saleId,
            ProductId = productId2,
            ProductName = "Cake",
            Quantity = 1m,
            UnitPrice = 25.000m
        };
        sale.AddItem(saleItem1);
        sale.AddItem(saleItem2);

        var product = CreateTestProduct();
        var inventory1 = CreateTestInventory(productId1, quantity: 10m);
        // Need both inventory items — but BuildServiceWithMocks only supports ONE inventory
        // For this test, we'll use the same product ID for simplicity

        // Use the same DefaultProductId for both items to work with single-inventory mock
        saleItem1.ProductId = DefaultProductId;
        saleItem2.ProductId = DefaultProductId;
        saleItem1.UnitPrice = 10.000m;
        saleItem2.UnitPrice = 25.000m;

        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m);
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { saleItem1, saleItem2 };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, shift, saleItems: saleItems);

        var returnItem1 = new ReturnItemRequest(item1Id, Quantity: 2m, "excess");
        var returnItem2 = new ReturnItemRequest(item2Id, Quantity: 1m, "not needed");

        // Act
        var result = await service.ReturnItemsAsync(
            saleId,
            new List<ReturnItemRequest> { returnItem1, returnItem2 },
            "Bulk return");

        // Assert
        result.Success.Should().BeTrue();

        // Total return = (10 * 2) + (25 * 1) = 20 + 25 = 45.000
        result.SuccessMessage.Should().Contain("45.000");

        // Return entity should have TotalAmount = 45.000
        unitOfWorkMock.Verify(u => u.Returns.AddAsync(
            It.Is<Return>(r => r.TotalAmount == 45.000m)), Times.Once);

        // Shift TotalReturns updated by 45.000
        unitOfWorkMock.Verify(u => u.Shifts.UpdateAsync(
            It.Is<Shift>(s => s.TotalReturns == 45.000m)), Times.Once);
    }

    [Fact]
    public async Task ReturnItemsAsync_NoInventoryRecord_StillCreatesReturn()
    {
        // Arrange — completed sale with item, but NO inventory record
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);
        sale.Status = SaleStatus.Completed;

        var saleItemId = Guid.NewGuid();
        var saleItem = new SaleItem
        {
            Id = saleItemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 1m,
            UnitPrice = 15.000m
        };
        sale.AddItem(saleItem);

        var product = CreateTestProduct();
        var shift = CreateTestShift(shiftId);
        var saleItems = new List<SaleItem> { saleItem };

        // No inventory passed → InventoryItems.FindAsync returns empty
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, shift: shift, saleItems: saleItems);

        var returnRequest = new ReturnItemRequest(saleItemId, Quantity: 1m, "no inventory");

        // Act
        var result = await service.ReturnItemsAsync(
            saleId,
            new List<ReturnItemRequest> { returnRequest },
            "Test return");

        // Assert
        result.Success.Should().BeTrue();

        // Return entity was still created
        unitOfWorkMock.Verify(u => u.Returns.AddAsync(
            It.IsAny<Return>()), Times.Once);

        // Inventory update should NOT happen (no inventory record)
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.IsAny<InventoryItem>()), Times.Never);

        // No inventory movement created
        unitOfWorkMock.Verify(u => u.InventoryMovements.AddAsync(
            It.IsAny<InventoryMovement>()), Times.Never);

        // Shift still gets updated
        unitOfWorkMock.Verify(u => u.Shifts.UpdateAsync(
            It.Is<Shift>(s => s.TotalReturns == 15.000m)), Times.Once);
    }

    // ========================================================================
    // RemoveItemAsync Tests
    // ========================================================================

    [Fact]
    public async Task RemoveItemAsync_HappyPath_RemovesItemAndReleasesInventory()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 3m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            TaxAmount = 4.800m,
            LineTotal = 34.800m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 30.000m;
        sale.TaxAmount = 4.800m;
        sale.TotalAmount = 34.800m;

        var product = CreateTestProduct();
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 3m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, saleItems: saleItems);

        // Act
        await service.RemoveItemAsync(saleId, itemId);

        // Assert
        // Inventory reserved quantity released: max(0, 3-3) = 0
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.ReservedQuantity == 0m)), Times.Once);

        // Sale item deleted
        unitOfWorkMock.Verify(u => u.SaleItems.DeleteAsync(
            It.Is<SaleItem>(si => si.Id == itemId)), Times.Once);

        // Sale updated with recalculated totals (0 after removal)
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.SubTotal == 0 && s.TotalAmount == 0)), Times.Once);

        // Transaction committed\r\n        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RemoveItemAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.RemoveItemAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0628\u064A\u0639 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task RemoveItemAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.RemoveItemAsync(saleId, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0644\u0627 \u064A\u0645\u0643\u0646 \u062D\u0630\u0641 \u0639\u0646\u0627\u0635\u0631 \u0645\u0646 \u0628\u064A\u0639 \u063A\u064A\u0631 \u0646\u0634\u0637");
    }

    [Fact]
    public async Task RemoveItemAsync_ItemNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — no saleItems passed, FindAsync returns empty
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.RemoveItemAsync(saleId, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0639\u0646\u0635\u0631 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task RemoveItemAsync_NoInventoryRecord_StillRemovesItem()
    {
        // Arrange — no inventory passed
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 1m,
            UnitPrice = 10.000m
        };
        sale.AddItem(item);

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        // Act
        await service.RemoveItemAsync(saleId, itemId);

        // Assert — item still deleted even without inventory
        unitOfWorkMock.Verify(u => u.SaleItems.DeleteAsync(
            It.Is<SaleItem>(si => si.Id == itemId)), Times.Once);

        // Inventory update should NOT happen
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.IsAny<InventoryItem>()), Times.Never);
    }

    // ========================================================================
    // UpdateItemQuantityAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdateItemQuantityAsync_IncreaseQty_UpdatesReservationAndTotals()
    {
        // Arrange — increase from 2 to 4
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 2m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var product = CreateTestProduct(price: 10.000m, taxRate: 0.16m);
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 2m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, saleItems: saleItems);

        // Act — increase qty by 2 (from 2 to 4)
        await service.UpdateItemQuantityAsync(saleId, itemId, 4m);

        // Assert
        // Inventory reservation increased by 2: 2 + (4-2) = 4
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.ReservedQuantity == 4m)), Times.Once);

        // Sale item updated with new quantity and recalculated totals
        unitOfWorkMock.Verify(u => u.SaleItems.UpdateAsync(
            It.Is<SaleItem>(si =>
                si.Quantity == 4m &&
                si.TaxAmount == 6.400m &&
                si.LineTotal == 46.400m)), Times.Once);

        // Sale updated with recalculated totals
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s =>
                s.SubTotal == 40.000m &&
                s.TaxAmount == 6.400m &&
                s.TotalAmount == 46.400m)), Times.Once);

        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_DecreaseQty_ReleasesReservation()
    {
        // Arrange — decrease from 5 to 2
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 5m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 8.000m,
            LineTotal = 58.000m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var product = CreateTestProduct(price: 10.000m, taxRate: 0.16m);
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 5m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, inventory, saleItems: saleItems);

        // Act — decrease qty by 3 (from 5 to 2)
        await service.UpdateItemQuantityAsync(saleId, itemId, 2m);

        // Assert
        // Reservation decreased by 3: 5 + (2-5) = 2
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.Is<InventoryItem>(inv => inv.ReservedQuantity == 2m)), Times.Once);

        // Line total recalculated for 2 units
        unitOfWorkMock.Verify(u => u.SaleItems.UpdateAsync(
            It.Is<SaleItem>(si =>
                si.Quantity == 2m &&
                si.TaxAmount == 3.200m &&
                si.LineTotal == 23.200m)), Times.Once);

        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_ZeroQuantity_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.UpdateItemQuantityAsync(Guid.NewGuid(), Guid.NewGuid(), 0m);

        // Assert — guard clause fires before any DB call
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0643\u0645\u064A\u0629 \u064A\u062C\u0628 \u0623\u0646 \u062A\u0643\u0648\u0646 \u0623\u0643\u0628\u0631 \u0645\u0646 \u0635\u0641\u0631");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_NegativeQuantity_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.UpdateItemQuantityAsync(Guid.NewGuid(), Guid.NewGuid(), -1m);

        // Assert — guard clause fires before any DB call
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0643\u0645\u064A\u0629 \u064A\u062C\u0628 \u0623\u0646 \u062A\u0643\u0648\u0646 \u0623\u0643\u0628\u0631 \u0645\u0646 \u0635\u0641\u0631");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act — 1 is valid as qty (passes guard clause), but sale lookup fails
        var act = () => service.UpdateItemQuantityAsync(Guid.NewGuid(), Guid.NewGuid(), 1m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0628\u064A\u0639 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.UpdateItemQuantityAsync(saleId, Guid.NewGuid(), 1m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0644\u0627 \u064A\u0645\u0643\u0646 \u062A\u0639\u062F\u064A\u0644 \u0628\u064A\u0639 \u063A\u064A\u0631 \u0646\u0634\u0637");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_ItemNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — no saleItems, FindAsync returns empty
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.UpdateItemQuantityAsync(saleId, Guid.NewGuid(), 2m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0639\u0646\u0635\u0631 \u063A\u064A\u0631 \u0645\u0648\u062C\u0648\u062F");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_InsufficientStockForIncrease_ThrowsInvalidOperationException()
    {
        // Arrange — try to increase qty by more than available
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 2m,
            UnitPrice = 10.000m
        };
        sale.AddItem(item);

        var product = CreateTestProduct();
        // Inventory: qty=10, reserved=2 → AvailableQuantity=8
        // Increase from 2 to 11 → qtyDiff=9, available=8 → insufficient!
        var inventory = CreateTestInventory(DefaultProductId, quantity: 10m, reservedQuantity: 2m);
        var saleItems = new List<SaleItem> { item };

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, inventory, saleItems: saleItems);

        // Act
        var act = () => service.UpdateItemQuantityAsync(saleId, itemId, 11m);

        // Assert — available = 10 - 2 = 8, need 9 more
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("\u0627\u0644\u0643\u0645\u064A\u0629 \u0627\u0644\u0645\u062A\u0627\u062D\u0629 \u063A\u064A\u0631 \u0643\u0627\u0641\u064A\u0629. \u0627\u0644\u0645\u062A\u0627\u062D: 8");
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_NoInventoryRecord_StillUpdatesItem()
    {
        // Arrange — no inventory record
        // NOTE: We DECREASE quantity to bypass the stock check guard clause
        // (when inventory is null, availableQty=0, so any increase would fail)
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Coffee",
            Quantity = 4m,  // Start at 4
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 6.400m,
            LineTotal = 46.400m
        };
        sale.AddItem(item);
        sale.SubTotal = 40.000m;
        sale.TaxAmount = 6.400m;
        sale.TotalAmount = 46.400m;

        var product = CreateTestProduct(price: 10.000m, taxRate: 0.16m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        // Act — DECREASE qty from 4 to 2 (qtyDiff = -2, bypasses stock check)
        await service.UpdateItemQuantityAsync(saleId, itemId, 2m);

        // Assert
        // Sale item updated with new quantity and recalculated totals
        unitOfWorkMock.Verify(u => u.SaleItems.UpdateAsync(
            It.Is<SaleItem>(si =>
                si.Quantity == 2m &&
                si.TaxAmount == 3.200m &&
                si.LineTotal == 23.200m)), Times.Once);

        // No inventory update (no record to update)
        unitOfWorkMock.Verify(u => u.InventoryItems.UpdateAsync(
            It.IsAny<InventoryItem>()), Times.Never);

        // Sale totals recalculated for 2 units
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s =>
                s.SubTotal == 20.000m &&
                s.TaxAmount == 3.200m &&
                s.TotalAmount == 23.200m)), Times.Once);

        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    // ========================================================================
    // ApplyDiscountAsync Tests
    // ========================================================================

    [Fact]
    public async Task ApplyDiscountAsync_HappyPath_AppliesDiscountAndUpdatesTotals()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, shiftId, userId);

        // Add an item so totals can be recalculated
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 2m,
            UnitPrice = 10.000m,
            TaxRate = DefaultTaxRate,
            Discount = 0,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = DefaultCost
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.DiscountAmount = 0;
        sale.TotalAmount = 23.200m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };
        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 5.000m, Reason: "Customer loyalty");

        // Act
        await service.ApplyDiscountAsync(request);

        // Assert
        sale.DiscountAmount.Should().Be(5.000m);

        // Recalculated totals: SubTotal=20.000, TaxAmount=3.200, Total=20.000+3.200-5.000=18.200
        sale.SubTotal.Should().Be(20.000m);
        sale.TaxAmount.Should().Be(3.200m);
        sale.TotalAmount.Should().Be(18.200m);

        // Sale was updated in DB
        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(
            It.Is<Sale>(s => s.DiscountAmount == 5.000m && s.TotalAmount == 18.200m)), Times.Once);

        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);

        // Audit was logged with DiscountApplied
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.DiscountApplied,
            "Sale",
            saleId,
            null,
            "Discount=5.000",
            "Customer loyalty"), Times.Once);
    }

    [Fact]
    public async Task ApplyDiscountAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        var request = new ApplyDiscountRequest(Guid.NewGuid(), DiscountAmount: 5.000m, Reason: null);

        // Act
        var act = () => service.ApplyDiscountAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    [Fact]
    public async Task ApplyDiscountAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange — sale is completed
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 5.000m, Reason: null);

        // Act
        var act = () => service.ApplyDiscountAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("لا يمكن تطبيق خصم على بيع غير نشط");
    }

    [Fact]
    public async Task ApplyDiscountAsync_ExceedsSubTotal_ThrowsInvalidOperationException()
    {
        // Arrange — discount (60) > subtotal (50)
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.SubTotal = 50.000m;

        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 60.000m, Reason: null);

        // Act
        var act = () => service.ApplyDiscountAsync(request);

        // Assert — SaleValidator returns "مبلغ الخصم يتجاوز المبلغ الإجمالي"
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("مبلغ الخصم يتجاوز المبلغ الإجمالي");
    }

    [Fact]
    public async Task ApplyDiscountAsync_NegativeDiscount_ThrowsInvalidOperationException()
    {
        // Arrange — discount < 0
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.SubTotal = 50.000m;

        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: -10.000m, Reason: null);

        // Act
        var act = () => service.ApplyDiscountAsync(request);

        // Assert — SaleValidator returns "مبلغ الخصم يجب أن يكون 0 أو أكبر"
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("مبلغ الخصم يجب أن يكون 0 أو أكبر");
    }

    [Fact]
    public async Task ApplyDiscountAsync_ZeroDiscount_IsValid()
    {
        // Arrange — zero discount is allowed
        var saleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, userId: userId);

        // Add an item so RecalculateSaleTotals doesn't zero everything out
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 5m,
            UnitPrice = 10.000m,
            TaxRate = DefaultTaxRate,
            Discount = 0,
            TaxAmount = 8.000m,
            LineTotal = 58.000m,
            Cost = DefaultCost
        };
        sale.AddItem(item);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };
        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 0m, Reason: "No discount");

        // Act
        await service.ApplyDiscountAsync(request);

        // Assert — discount is 0, totals unchanged
        sale.DiscountAmount.Should().Be(0m);
        sale.SubTotal.Should().Be(50.000m);
        sale.TaxAmount.Should().Be(8.000m);
        sale.TotalAmount.Should().Be(58.000m);

        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(It.IsAny<Sale>()), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);

        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.DiscountApplied,
            "Sale",
            saleId,
            null,
            "Discount=0",
            "No discount"), Times.Once);
    }

    [Fact]
    public async Task ApplyDiscountAsync_RecalculatesTotalAmountWithMultipleItems()
    {
        // Arrange — 2 items, discount applied, verify recalculated TotalAmount
        var saleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, userId: userId);

        var item1 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Item 1",
            Quantity = 3m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m,
            Discount = 0,
            TaxAmount = 4.800m,
            LineTotal = 34.800m,
            Cost = 5.000m
        };
        var item2 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Item 2",
            Quantity = 1m,
            UnitPrice = 25.000m,
            TaxRate = 0.16m,
            Discount = 0,
            TaxAmount = 4.000m,
            LineTotal = 29.000m,
            Cost = 12.000m
        };

        sale.AddItem(item1);
        sale.AddItem(item2);
        // Pre-calculated: (10*3)=30 + (25*1)=25 → SubTotal=55.000
        // Tax: Round(30*0.16)=4.800 + Round(25*0.16)=4.000 → TaxAmount=8.800
        // Total without discount = 55.000 + 8.800 = 63.800
        sale.SubTotal = 55.000m;
        sale.TaxAmount = 8.800m;
        sale.TotalAmount = 63.800m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item1, item2 };
        var (service, _, _) = BuildServiceWithMocks(sale, product, saleItems: saleItems);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 10.000m, Reason: "Bulk discount");

        // Act
        await service.ApplyDiscountAsync(request);

        // Assert — TotalAmount = 55.000 + 8.800 - 10.000 = 53.800
        sale.DiscountAmount.Should().Be(10.000m);
        sale.SubTotal.Should().Be(55.000m);
        sale.TaxAmount.Should().Be(8.800m);
        sale.TotalAmount.Should().Be(53.800m);
    }

    [Fact]
    public async Task ApplyDiscountAsync_WithoutReason_AllowsNullReason()
    {
        // Arrange — Reason parameter is nullable
        var saleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, userId: userId);

        // Add an item so RecalculateSaleTotals has data to work with
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "Test Product",
            Quantity = 5m,
            UnitPrice = 10.000m,
            TaxRate = DefaultTaxRate,
            Discount = 0,
            TaxAmount = 8.000m,
            LineTotal = 58.000m,
            Cost = DefaultCost
        };
        sale.AddItem(item);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.TotalAmount = 58.000m;

        var product = CreateTestProduct();
        var saleItems = new List<SaleItem> { item };
        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems);

        var request = new ApplyDiscountRequest(saleId, DiscountAmount: 5.000m, Reason: null);

        // Act
        await service.ApplyDiscountAsync(request);

        // Assert — audit logged with null reason; totals recalculated
        sale.DiscountAmount.Should().Be(5.000m);
        sale.SubTotal.Should().Be(50.000m);
        sale.TaxAmount.Should().Be(8.000m);
        sale.TotalAmount.Should().Be(53.000m); // 50 + 8 - 5

        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.DiscountApplied,
            "Sale",
            saleId,
            null,
            "Discount=5.000",
            null), Times.Once);

        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(It.IsAny<Sale>()), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    // ========================================================================
    // CreateNewSaleAsync Tests
    // ========================================================================

    [Fact]
    public async Task CreateNewSaleAsync_DineIn_WithTable_CreatesSaleWithCorrectProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var tableId = Guid.NewGuid();

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null, existingSales: new List<Sale>());

        // Act
        var saleId = await service.CreateNewSaleAsync(userId, shiftId, "DineIn", tableId);

        // Assert — returned ID is non-empty
        saleId.Should().NotBe(Guid.Empty);

        // Verify the sale was added with correct properties
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s =>
                s.Id == saleId &&
                s.UserId == userId &&
                s.ShiftId == shiftId &&
                s.OrderType == OrderType.DineIn &&
                s.TableId == tableId &&
                s.Status == SaleStatus.Active &&
                s.SubTotal == 0 &&
                s.TaxAmount == 0 &&
                s.DiscountAmount == 0 &&
                s.TotalAmount == 0 &&
                s.IsPaid == false &&
                s.InvoiceNumber.StartsWith("INV-") &&
                s.InvoiceNumber.EndsWith("-0001"))), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_InvoiceNumber_SequencesCorrectly()
    {
        // Arrange — 2 existing sales with today's date prefix
        var today = DateTime.Now.ToString("yyyyMMdd");
        var existingSales = new List<Sale>
        {
            new() { InvoiceNumber = $"INV-{today}-0001" },
            new() { InvoiceNumber = $"INV-{today}-0002" }
        };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null, existingSales: existingSales);

        // Act
        var saleId = await service.CreateNewSaleAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert — sequence should be 3 (count + 1 = 2 + 1 = 3)
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.InvoiceNumber == $"INV-{today}-0003")), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_OrderType_DefaultsToTakeaway_WhenNull()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act — orderType not provided (defaults to null)
        var saleId = await service.CreateNewSaleAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert — falls back to Takeaway
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.OrderType == OrderType.Takeaway)), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_OrderTypeDelivery_ParsesCorrectly()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act
        var saleId = await service.CreateNewSaleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Delivery");

        // Assert — parsed to Delivery
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.OrderType == OrderType.Delivery)), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_OrderType_CaseInsensitive()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act — lowercase "dinein" should still parse
        var saleId = await service.CreateNewSaleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "dinein");

        // Assert — Enum.TryParse with ignoreCase: true
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.OrderType == OrderType.DineIn)), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_OrderType_Invalid_FallsBackToTakeaway()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act — invalid order type string
        var saleId = await service.CreateNewSaleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "InvalidOrderType");

        // Assert — TryParse fails, falls back to Takeaway
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.OrderType == OrderType.Takeaway)), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_TableId_Null_WhenNotProvided()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act — tableId not provided (defaults to null)
        var saleId = await service.CreateNewSaleAsync(Guid.NewGuid(), Guid.NewGuid(), "DineIn");

        // Assert — TableId stays null even though order type is DineIn
        unitOfWorkMock.Verify(u => u.Sales.AddAsync(
            It.Is<Sale>(s => s.TableId == null)), Times.Once);
    }

    [Fact]
    public async Task CreateNewSaleAsync_ReturnsNonEmptyGuid()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(
            sale: null, product: null);

        // Act
        var saleId = await service.CreateNewSaleAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert — Sale.Id is set by the Sale constructor/new Guid() before AddAsync
        saleId.Should().NotBe(Guid.Empty);
    }

    // ========================================================================
    // GetSalesHistoryAsync — Sales History with Date Range & Pagination
    // ========================================================================

    [Fact]
    public async Task GetSalesHistoryAsync_NoFilter_ReturnsAllOrderedDescending()
    {
        // Arrange
        var sale1 = CreateActiveSale(Guid.NewGuid());
        sale1.CreatedAt = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);
        sale1.SubTotal = 50.000m;

        var sale2 = CreateActiveSale(Guid.NewGuid());
        sale2.CreatedAt = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
        sale2.SubTotal = 100.000m;

        var sale3 = CreateActiveSale(Guid.NewGuid());
        sale3.CreatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        sale3.SubTotal = 75.000m;

        // Build mocks with existing sales for GetAllAsync
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);
        // We need to pass existing sales through the sales repo mock
        // Rebuild with existing sales
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();
        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var sales = new List<Sale> { sale1, sale2, sale3 };
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(sales);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // Stub remaining repos
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var svc = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act
        var result = await svc.GetSalesHistoryAsync(null, null);

        // Assert — ordered descending
        result.Should().HaveCount(3);
        result[0].SubTotal.Should().Be(75.000m);  // July 20
        result[1].SubTotal.Should().Be(100.000m); // July 19
        result[2].SubTotal.Should().Be(50.000m);  // July 18
    }

    [Fact]
    public async Task GetSalesHistoryAsync_DateRange_FiltersCorrectly()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();
        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var sale1 = CreateActiveSale(Guid.NewGuid());
        sale1.CreatedAt = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc); // before "from"
        var sale2 = CreateActiveSale(Guid.NewGuid());
        sale2.CreatedAt = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc); // in range
        var sale3 = CreateActiveSale(Guid.NewGuid());
        sale3.CreatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc); // after "to"+1

        var sales = new List<Sale> { sale1, sale2, sale3 };
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(sales);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var svc = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        var from = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await svc.GetSalesHistoryAsync(from, to);

        // Assert — only sale2 (July 19) is within range (to+1d = July 20 00:00)
        result.Should().HaveCount(1);
        result[0].SubTotal.Should().Be(sale2.SubTotal);
    }

    [Fact]
    public async Task GetSalesHistoryAsync_Pagination_RespectsPageSize()
    {
        // Arrange — 7 sales, page 1 size 3
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();
        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var sales = Enumerable.Range(1, 7)
            .Select(i =>
            {
                var s = CreateActiveSale(Guid.NewGuid());
                s.CreatedAt = new DateTime(2026, 7, 10 + i, 8, 0, 0, DateTimeKind.Utc);
                s.SubTotal = i * 10m;
                return s;
            }).ToList();

        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(sales);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var svc = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act — page 1 (3 items), page 3 (last page: 1 item)
        var page1 = await svc.GetSalesHistoryAsync(null, null, page: 1, pageSize: 3);
        var page3 = await svc.GetSalesHistoryAsync(null, null, page: 3, pageSize: 3);

        // Assert
        page1.Should().HaveCount(3);
        page3.Should().HaveCount(1); // 7 - (2*3) = 1
    }

    [Fact]
    public async Task GetSalesHistoryAsync_Empty_ReturnsEmptyList()
    {
        // Arrange — no sales
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();
        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Sale>());
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);

        var svc = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act
        var result = await svc.GetSalesHistoryAsync(null, null);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetSaleSummaryAsync — Single Sale Summary
    // ========================================================================

    [Fact]
    public async Task GetSaleSummaryAsync_SaleExists_ReturnsSummaryDto()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        sale.SubTotal = 50.000m;
        sale.TaxAmount = 8.000m;
        sale.DiscountAmount = 5.000m;
        sale.TotalAmount = 53.000m;

        var product = CreateTestProduct();
        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.GetSaleSummaryAsync(saleId);

        // Assert
        result.SaleId.Should().Be(saleId);
        result.SubTotal.Should().Be(50.000m);
        result.TaxAmount.Should().Be(8.000m);
        result.DiscountAmount.Should().Be(5.000m);
        result.TotalAmount.Should().Be(53.000m);
        result.Status.Should().Be("Active");
        result.InvoiceNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSaleSummaryAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale returns null
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.GetSaleSummaryAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    // ========================================================================
    // GetSaleItemsAsync — Sale Items List
    // ========================================================================

    [Fact]
    public async Task GetSaleItemsAsync_ItemsExist_ReturnsMappedDtos()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();

        var item1 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 2m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = 5.000m,
            Notes = null,
            ModifierSummary = null
        };
        var item2 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "شاي",
            Quantity = 1m,
            UnitPrice = 5.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 0.800m,
            LineTotal = 5.800m,
            Cost = 2.000m,
            Notes = null,
            ModifierSummary = null
        };

        var saleItems = new List<SaleItem> { item1, item2 };
        var (service, _, _) = BuildServiceWithMocks(sale, product, saleItems: saleItems);

        // Act
        var result = await service.GetSaleItemsAsync(saleId);

        // Assert
        result.Should().HaveCount(2);
        result[0].ProductName.Should().Be("قهوة");
        result[0].Quantity.Should().Be(2m);
        result[0].UnitPrice.Should().Be(10.000m);
        result[1].ProductName.Should().Be("شاي");
        result[1].Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task GetSaleItemsAsync_NoItems_ReturnsEmptyList()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var result = await service.GetSaleItemsAsync(saleId);

        // Assert — no saleItems passed, FindAsync returns empty
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetHeldSalesAsync — Held Sales List
    // ========================================================================

    [Fact]
    public async Task GetHeldSalesAsync_HeldSalesExist_ReturnsDtosWithParsedTotals()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var heldSaleId = Guid.NewGuid();

        var serializedData = System.Text.Json.JsonSerializer.Serialize(new
        {
            SaleId = Guid.NewGuid(),
            TotalAmount = 58.000m,
            Items = new List<object>()
        });

        var heldSale = new HeldSale
        {
            Id = heldSaleId,
            SerializedData = serializedData,
            ShiftId = shiftId,
            UserId = Guid.NewGuid(),
            HoldReason = "Customer left",
            CreatedAt = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc)
        };

        var (service, _, _) = BuildServiceWithMocks(
            sale: null, product: null,
            heldSales: new List<HeldSale> { heldSale });

        // Act
        var result = await service.GetHeldSalesAsync(shiftId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(heldSaleId);
        result[0].HoldReason.Should().Be("Customer left");
        result[0].TotalAmount.Should().Be(58.000m);
    }

    [Fact]
    public async Task GetHeldSalesAsync_MalformedJson_ReturnsZeroTotal()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var heldSale = new HeldSale
        {
            Id = Guid.NewGuid(),
            SerializedData = "invalid json!!",
            ShiftId = shiftId,
            HoldReason = "broken",
            CreatedAt = DateTime.UtcNow
        };

        var (service, _, _) = BuildServiceWithMocks(
            sale: null, product: null,
            heldSales: new List<HeldSale> { heldSale });

        // Act
        var result = await service.GetHeldSalesAsync(shiftId);

        // Assert — TotalAmount defaults to 0
        result.Should().HaveCount(1);
        result[0].TotalAmount.Should().Be(0);
        result[0].HoldReason.Should().Be("broken");
    }

    [Fact]
    public async Task GetHeldSalesAsync_NoHeldSales_ReturnsEmptyList()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var result = await service.GetHeldSalesAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // ModifyItemAsync — Modify Item Modifiers
    // ========================================================================

    [Fact]
    public async Task ModifyItemAsync_HappyPath_ReplacesModifiersAndRecalculates()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId, userId: userId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 2m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 3.200m,
            LineTotal = 23.200m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 20.000m;
        sale.TaxAmount = 3.200m;
        sale.TotalAmount = 23.200m;

        var product = CreateTestProduct();
        var modifierId = Guid.NewGuid();
        var modifier = CreateTestModifier(modifierId, "Extra Cream", price: 1.500m);
        var saleItems = new List<SaleItem> { item };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems,
            modifiers: new List<Modifier> { modifier });

        var modifiers = new[]
        {
            new ModifierSelectionDto(modifierId, ModifierSizeId: null, Quantity: 2)
        };

        // Act
        var result = await service.ModifyItemAsync(saleId, itemId, modifiers);

        // Assert — returned DTO
        result.Should().NotBeNull();
        result.ProductName.Should().Be("قهوة");
        result.ModifierSummary.Should().Be("Extra Cream");

        // Sale totals recalculated by RecalculateSaleTotals — now includes modifiers
        // modifier: Extra Cream 1.500 * 2 qty = 3.000
        // lineBeforeTax=Round(10*2 + 3.000 - 0)=23.000, tax=Round(23.000*0.16)=3.680, lineTotal=26.680
        // SubTotal=23.000, TaxAmount=3.680, TotalAmount=23.000+3.680-0=26.680
        sale.SubTotal.Should().Be(23.000m);
        sale.TaxAmount.Should().Be(3.680m);
        sale.TotalAmount.Should().Be(26.680m);

        // No old modifiers to delete (item was created without modifiers)
        // New modifier was added (AdditionalPrice is per-unit = 1.500, Quantity = 2)
        unitOfWorkMock.Verify(u => u.SaleItemModifiers.AddAsync(
            It.Is<SaleItemModifier>(m => m.ModifierName == "Extra Cream" && m.AdditionalPrice == 1.500m)), Times.Once);

        unitOfWorkMock.Verify(u => u.Sales.UpdateAsync(It.IsAny<Sale>()), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ModifyItemAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var act = () => service.ModifyItemAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<ModifierSelectionDto>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    [Fact]
    public async Task ModifyItemAsync_SaleNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateNonActiveSale(saleId, SaleStatus.Completed);
        var product = CreateTestProduct();

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.ModifyItemAsync(saleId, Guid.NewGuid(), Array.Empty<ModifierSelectionDto>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("لا يمكن تعديل بيع غير نشط");
    }

    [Fact]
    public async Task ModifyItemAsync_ItemNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale exists but no saleItems
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);
        var product = CreateTestProduct();

        var (service, _, _) = BuildServiceWithMocks(sale, product);

        // Act
        var act = () => service.ModifyItemAsync(saleId, Guid.NewGuid(), Array.Empty<ModifierSelectionDto>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العنصر غير موجود");
    }

    [Fact]
    public async Task ModifyItemAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — sale + item exist but productId doesn't match any product
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 1m,
            UnitPrice = 10.000m,
            TaxRate = 0.16m
        };
        sale.AddItem(item);

        var saleItems = new List<SaleItem> { item };
        // product is null → GetByIdAsync returns null
        var (service, _, _) = BuildServiceWithMocks(sale, product: null, saleItems: saleItems);

        // Act
        var act = () => service.ModifyItemAsync(saleId, itemId, Array.Empty<ModifierSelectionDto>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المنتج غير موجود");
    }

    [Fact]
    public async Task ModifyItemAsync_WithModifierSize_IncludesSizeAdjustment()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 1m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 1.600m,
            LineTotal = 11.600m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 10.000m;
        sale.TaxAmount = 1.600m;
        sale.TotalAmount = 11.600m;

        var product = CreateTestProduct();
        var modifierId = Guid.NewGuid();
        var modifier = CreateTestModifier(modifierId, "Extra Cheese", price: 2.000m);
        var modSize = CreateTestModifierSize(modifierId, priceAdjustment: 1.500m);
        var saleItems = new List<SaleItem> { item };

        var (service, _, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems,
            modifiers: new List<Modifier> { modifier },
            modifierSizes: new List<ModifierSize> { modSize });

        var modifiers = new[]
        {
            new ModifierSelectionDto(modifierId, ModifierSizeId: modSize.Id, Quantity: 1)
        };

        // Act
        var result = await service.ModifyItemAsync(saleId, itemId, modifiers);

        // Assert — modifier price (2.000) + size adjustment (1.500) = 3.500/unit
        result.ModifierSummary.Should().Be("Extra Cheese");

        // Sale totals recalculated by RecalculateSaleTotals — now includes modifiers
        // modifierExtra = 3.500 * 1 = 3.500
        // lineBeforeTax=Round(10*1 + 3.500 - 0)=13.500, tax=Round(13.500*0.16)=2.160, lineTotal=15.660
        // SubTotal=13.500, TaxAmount=2.160, TotalAmount=13.500+2.160-0=15.660
        sale.SubTotal.Should().Be(13.500m);
        sale.TaxAmount.Should().Be(2.160m);
        sale.TotalAmount.Should().Be(15.660m);
    }

    [Fact]
    public async Task ModifyItemAsync_RemovesOldModifiersAndAddsNewOnes()
    {
        // Arrange — item has old modifier, should be deleted
        var saleId = Guid.NewGuid();
        var sale = CreateActiveSale(saleId);

        var itemId = Guid.NewGuid();
        var item = new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = DefaultProductId,
            ProductName = "قهوة",
            Quantity = 1m,
            UnitPrice = 10.000m,
            Discount = 0,
            TaxRate = 0.16m,
            TaxAmount = 1.600m,
            LineTotal = 11.600m,
            Cost = 5.000m
        };
        sale.AddItem(item);
        sale.SubTotal = 10.000m;
        sale.TaxAmount = 1.600m;
        sale.TotalAmount = 11.600m;

        var product = CreateTestProduct();
        var modifierId = Guid.NewGuid();
        var modifier = CreateTestModifier(modifierId, "Sugar", price: 0.500m);
        var saleItems = new List<SaleItem> { item };

        // Create an old modifier on the item
        var oldMod = new SaleItemModifier
        {
            Id = Guid.NewGuid(),
            SaleItemId = itemId,
            ModifierId = Guid.NewGuid(),
            ModifierName = "Old Mod",
            AdditionalPrice = 1.000m,
            Quantity = 1
        };
        item.AddModifier(oldMod);

        // Mock SaleItemModifiers.FindAsync to return the old modifier
        var simList = new List<SaleItemModifier> { oldMod };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            sale, product, saleItems: saleItems,
            modifiers: new List<Modifier> { modifier });

        // The mock builder already stubs SaleItemModifiers with CreateEmptyRepoMock
        // We need to override it for this test
        // Rebuild manually for the SaleItemModifiers override
        var unitOfWorkMock2 = new Mock<IUnitOfWork>();
        var auditServiceMock2 = new Mock<IAuditService>();
        auditServiceMock2
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock2.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(sale);
        saleRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Sale>())).Returns(Task.CompletedTask);
        unitOfWorkMock2.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(product);
        unitOfWorkMock2.Setup(u => u.Products).Returns(productRepoMock.Object);

        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync((Expression<Func<SaleItem, bool>> predicate) =>
                saleItems.AsQueryable().Where(predicate).ToList());
        unitOfWorkMock2.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        var simRepoMock = new Mock<IRepository<SaleItemModifier>>();
        simRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItemModifier, bool>>>()))
            .ReturnsAsync(simList);
        simRepoMock.Setup(r => r.DeleteAsync(It.IsAny<SaleItemModifier>())).Returns(Task.CompletedTask);
        simRepoMock.Setup(r => r.AddAsync(It.IsAny<SaleItemModifier>())).Returns(Task.CompletedTask);
        unitOfWorkMock2.Setup(u => u.SaleItemModifiers).Returns(simRepoMock.Object);

        // Modifiers repo — must return the modifier for GetByIdAsync
        var modRepoMock = new Mock<IRepository<Modifier>>();
        modRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(modifier);
        unitOfWorkMock2.Setup(u => u.Modifiers).Returns(modRepoMock.Object);

        // Stub remaining
        unitOfWorkMock2.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock2.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock2.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock2.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock2.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock2.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock2.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock2.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock2.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock2.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock2.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock2.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        unitOfWorkMock2.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock2.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock2.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock2.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock2.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock2.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock2.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock2.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock2.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock2.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock2.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock2.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock2.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var svc = new SaleService(unitOfWorkMock2.Object, auditServiceMock2.Object);

        var modifiers = new[]
        {
            new ModifierSelectionDto(modifierId, ModifierSizeId: null, Quantity: 1)
        };

        // Act
        var result = await svc.ModifyItemAsync(saleId, itemId, modifiers);

        // Assert — old modifier was deleted via FindAsync+DeleteAsync
        simRepoMock.Verify(r => r.DeleteAsync(It.Is<SaleItemModifier>(m => m.ModifierName == "Old Mod")), Times.Once);

        // New modifier was added
        simRepoMock.Verify(r => r.AddAsync(
            It.Is<SaleItemModifier>(m => m.ModifierName == "Sugar")), Times.Once);

        // Item's modifiers collection has old + new (code only deletes via repo, not from collection)
        item.Modifiers.Should().HaveCount(2);
        item.Modifiers.Should().Contain(m => m.ModifierName == "Old Mod");
        item.Modifiers.Should().Contain(m => m.ModifierName == "Sugar");
    }

    // ========================================================================
    // GetAppliedPromotionsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetAppliedPromotionsAsync_NoPromotions_ReturnsEmpty()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks(sale: null, product: null);

        // Act
        var result = await service.GetAppliedPromotionsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAppliedPromotionsAsync_HasPromotions_ReturnsOrderedList()
    {
        // Arrange — use manual mock with SalePromotions setup
        var saleId = Guid.NewGuid();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var promotions = new List<SalePromotion>
        {
            new() { SaleId = saleId, PromotionId = Guid.NewGuid(), DiscountAmount = 5.000m, Description = "الخصم الأول", CreatedAt = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc) },
            new() { SaleId = saleId, PromotionId = Guid.NewGuid(), DiscountAmount = 10.000m, Description = "الخصم الثاني", CreatedAt = new DateTime(2026, 7, 19, 11, 0, 0, DateTimeKind.Utc) }
        };

        var salePromoRepoMock = new Mock<IRepository<SalePromotion>>();
        salePromoRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SalePromotion, bool>>>()))
            .ReturnsAsync(promotions);
        unitOfWorkMock.Setup(u => u.SalePromotions).Returns(salePromoRepoMock.Object);

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var service = new SaleService(unitOfWorkMock.Object, auditServiceMock.Object);

        // Act
        var result = await service.GetAppliedPromotionsAsync(saleId);

        // Assert — ordered by CreatedAt
        result.Should().HaveCount(2);
        result[0].DiscountAmount.Should().Be(5.000m);
        result[0].Name.Should().Be("الخصم الأول");
        result[1].DiscountAmount.Should().Be(10.000m);
        result[1].Name.Should().Be("الخصم الثاني");
    }
}



