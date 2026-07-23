#nullable enable

using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;
using POS.Infrastructure.Repositories;

namespace POS.Tests.IntegrationTests;

/// <summary>
/// Test-only implementation of IUnitOfWork that uses real Repository{T} instances
/// backed by a POSDbContext (configured with EF Core InMemory provider).
/// Transaction methods are no-ops because InMemory does not support IDbContextTransaction.
/// 
/// This enables SaleService integration tests with a real DbContext, real Repositories,
/// real IAuditService (mocked), and full OnModelCreating behavior — no Moq on IUnitOfWork.
/// </summary>
public sealed class TestUnitOfWork : IUnitOfWork
{
    private readonly POSDbContext _context;

    public TestUnitOfWork(POSDbContext context)
    {
        _context = context;

        Users = new Repository<User>(_context);
        Categories = new Repository<Category>(_context);
        Products = new Repository<Product>(_context);
        InventoryItems = new Repository<InventoryItem>(_context);
        Recipes = new Repository<Recipe>(_context);
        RecipeIngredients = new Repository<RecipeIngredient>(_context);
        ModifierGroups = new Repository<ModifierGroup>(_context);
        Modifiers = new Repository<Modifier>(_context);
        ModifierSizes = new Repository<ModifierSize>(_context);
        Tables = new Repository<Table>(_context);
        Rooms = new Repository<Room>(_context);
        KitchenStations = new Repository<KitchenStation>(_context);
        Sales = new Repository<Sale>(_context);
        SaleItems = new Repository<SaleItem>(_context);
        SaleItemModifiers = new Repository<SaleItemModifier>(_context);
        Payments = new Repository<Payment>(_context);
        Customers = new Repository<Customer>(_context);
        Suppliers = new Repository<Supplier>(_context);
        PurchaseOrders = new Repository<PurchaseOrder>(_context);
        PurchaseOrderItems = new Repository<PurchaseOrderItem>(_context);
        InventoryMovements = new Repository<InventoryMovement>(_context);
        Shifts = new Repository<Shift>(_context);
        Expenses = new Repository<Expense>(_context);
        WithdrawalDeposits = new Repository<WithdrawalDeposit>(_context);
        Printers = new Repository<Printer>(_context);
        Registers = new Repository<Register>(_context);
        Settings = new Repository<Setting>(_context);
        HeldSales = new Repository<HeldSale>(_context);
        Returns = new Repository<Return>(_context);
        ReturnItems = new Repository<ReturnItem>(_context);
        Promotions = new Repository<Promotion>(_context);
        SalePromotions = new Repository<SalePromotion>(_context);
        InventoryBatches = new Repository<InventoryBatch>(_context);
        AuditLogs = new SimpleRepository<AuditLog>(_context);
        BackupRecords = new SimpleRepository<BackupRecord>(_context);
        UnitOfMeasures = new Repository<UnitOfMeasure>(_context);
    }

    // --- Repository Properties ---
    public IRepository<User> Users { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Product> Products { get; }
    public IRepository<InventoryItem> InventoryItems { get; }
    public IRepository<Recipe> Recipes { get; }
    public IRepository<RecipeIngredient> RecipeIngredients { get; }
    public IRepository<ModifierGroup> ModifierGroups { get; }
    public IRepository<Modifier> Modifiers { get; }
    public IRepository<ModifierSize> ModifierSizes { get; }
    public IRepository<Table> Tables { get; }
    public IRepository<Room> Rooms { get; }
    public IRepository<KitchenStation> KitchenStations { get; }
    public IRepository<Sale> Sales { get; }
    public IRepository<SaleItem> SaleItems { get; }
    public IRepository<SaleItemModifier> SaleItemModifiers { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<Customer> Customers { get; }
    public IRepository<Supplier> Suppliers { get; }
    public IRepository<PurchaseOrder> PurchaseOrders { get; }
    public IRepository<PurchaseOrderItem> PurchaseOrderItems { get; }
    public IRepository<InventoryMovement> InventoryMovements { get; }
    public IRepository<Shift> Shifts { get; }
    public IRepository<Expense> Expenses { get; }
    public IRepository<WithdrawalDeposit> WithdrawalDeposits { get; }
    public IRepository<Printer> Printers { get; }
    public IRepository<Register> Registers { get; }
    public IRepository<Setting> Settings { get; }
    public IRepository<HeldSale> HeldSales { get; }
    public IRepository<Return> Returns { get; }
    public IRepository<ReturnItem> ReturnItems { get; }
    public IRepository<Promotion> Promotions { get; }
    public IRepository<SalePromotion> SalePromotions { get; }
    public IRepository<InventoryBatch> InventoryBatches { get; }
    public ISimpleRepository<AuditLog> AuditLogs { get; }
    public ISimpleRepository<BackupRecord> BackupRecords { get; }
    public IRepository<UnitOfMeasure> UnitOfMeasures { get; }

    // --- Transaction methods (InMemory does not support real transactions,
    //      but CommitAsync must still persist the unit of work) ---
    public Task BeginTransactionAsync() => Task.CompletedTask;
    public async Task CommitAsync() => await _context.SaveChangesAsync();
    public Task RollbackAsync() => Task.CompletedTask;
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public async Task<bool> CanConnectAsync() => await _context.Database.CanConnectAsync();
}

/// <summary>
/// Integration tests for SaleService using EF Core InMemory with real POSDbContext + Repositories.
/// These tests exercise real OnModelCreating, soft-delete filters, inventory calculations,
/// invoice numbering, hold/retrieve serialization, and CRUD operations against a real DbContext.
/// </summary>
public sealed class SaleServiceIntegrationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RegisterId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    // ========================================================================
    // Fixture setup
    // ========================================================================

    /// <summary>
    /// Creates a fresh POSDbContext with EF Core InMemory, seeds the database
    /// with baseline entities (user, shift, product, category, inventory), and
    /// returns a SaleService wired to the real DbContext via TestUnitOfWork.
    /// </summary>
    private (SaleService service, POSDbContext context) CreateService()
    {
        var dbName = $"POS_SaleTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new POSDbContext(options);
        var unitOfWork = new TestUnitOfWork(context);
        var auditMock = new Mock<IAuditService>();
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var service = new SaleService(unitOfWork, auditMock.Object);

        // Seed baseline data
        SeedBaseline(context).GetAwaiter().GetResult();

        return (service, context);
    }

    /// <summary>
    /// Seeds the minimal entities required for SaleService to function:
    /// a category, a product, an inventory record, a shift, and a user.
    /// </summary>
    private async Task SeedBaseline(POSDbContext context)
    {
        var user = new User
        {
            Id = UserId,
            Username = "testuser",
            FullName = "Test User",
            Role = UserRole.Cashier,
            IsActive = true
        };
        context.Users.Add(user);

        var category = new Category
        {
            Id = CategoryId,
            Name = "Test Category",
            IsActive = true
        };
        context.Categories.Add(category);

        var product = new Product
        {
            Id = ProductId,
            Name = "Test Product",
            ArabicName = "منتج اختباري",
            Sku = "TST-001",
            Price = 10.000m,
            Cost = 5.000m,
            TaxRate = 0.16m,
            MinStock = 5m,
            CategoryId = CategoryId,
            Status = ProductStatus.Active
        };
        context.Products.Add(product);

        var inventory = new InventoryItem
        {
            ProductId = ProductId,
            Name = "Test Product Inv",
            Quantity = 100m,
            ReservedQuantity = 0,
            Cost = 5.000m,
            Unit = "piece"
        };
        context.InventoryItems.Add(inventory);

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            ShiftNumber = 1,
            UserId = UserId,
            RegisterId = RegisterId,
            OpeningCash = 500.000m,
            Status = ShiftStatus.Open,
            OpenedAt = DateTime.UtcNow
        };
        context.Shifts.Add(shift);

        var register = new Register
        {
            Id = RegisterId,
            Name = "Test Register",
            IsActive = true
        };
        context.Registers.Add(register);

        await context.SaveChangesAsync();
    }

    private Guid GetShiftId(POSDbContext context) =>
        context.Shifts.First().Id;

    // ========================================================================
    // CreateNewSaleAsync — Invoice Numbering & Order Type
    // ========================================================================

    [Fact]
    public async Task CreateNewSaleAsync_CreatesSale_WithCorrectInvoiceNumber()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);

        // Act
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);

        // Assert
        var sale = await context.Sales.FindAsync(saleId);
        sale.Should().NotBeNull();
        sale!.InvoiceNumber.Should().StartWith("INV-");
        sale.InvoiceNumber.Should().Contain(DateTime.Now.ToString("yyyyMMdd"));
        sale.Status.Should().Be(SaleStatus.Active);
        sale.SubTotal.Should().Be(0);
        sale.TotalAmount.Should().Be(0);
        sale.IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateNewSaleAsync_DefaultOrderType_IsTakeaway()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);

        // Act
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);

        // Assert
        var sale = await context.Sales.FindAsync(saleId);
        sale!.OrderType.Should().Be(OrderType.Takeaway);
    }

    [Fact]
    public async Task CreateNewSaleAsync_WithDineInOrderType_ParsesCorrectly()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);

        // Act
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId, orderType: "DineIn");

        // Assert
        var sale = await context.Sales.FindAsync(saleId);
        sale!.OrderType.Should().Be(OrderType.DineIn);
    }

    [Fact]
    public async Task CreateNewSaleAsync_InvalidOrderType_FallsBackToTakeaway()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);

        // Act
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId, orderType: "InvalidType");

        // Assert
        var sale = await context.Sales.FindAsync(saleId);
        sale!.OrderType.Should().Be(OrderType.Takeaway);
    }

    [Fact]
    public async Task CreateNewSaleAsync_IncrementsInvoiceSequence()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);

        // Act — create 2 sales
        var saleId1 = await service.CreateNewSaleAsync(UserId, shiftId);
        var saleId2 = await service.CreateNewSaleAsync(UserId, shiftId);

        // Assert — sequential invoice numbers
        var sale1 = await context.Sales.FindAsync(saleId1);
        var sale2 = await context.Sales.FindAsync(saleId2);
        sale1!.InvoiceNumber.Should().NotBe(sale2!.InvoiceNumber);
    }

    // ========================================================================
    // AddItemAsync — Adding Items & Inventory Reservation
    // ========================================================================

    [Fact]
    public async Task AddItemAsync_AddsItem_AndReservesInventory()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);

        var request = new AddItemRequest(ProductId, Quantity: 2m, Notes: null, Modifiers: null);

        // Act
        await service.AddItemAsync(saleId, request);

        // Assert — item was added
        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        items.Should().HaveCount(1);
        items[0].ProductId.Should().Be(ProductId);
        items[0].Quantity.Should().Be(2m);
        items[0].ProductName.Should().Be("منتج اختباري");
        items[0].UnitPrice.Should().Be(10.000m);
        items[0].TaxRate.Should().Be(0.16m);

        // LineTotal: (10 * 2) + (10 * 2 * 0.16) = 20 + 3.200 = 23.200
        items[0].LineTotal.Should().Be(23.200m);
        items[0].TaxAmount.Should().Be(3.200m);

        // Assert — inventory was reserved
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.ReservedQuantity.Should().Be(2m);
        inventory.AvailableQuantity.Should().Be(98m);

        // Assert — sale totals recalculated
        var sale = await context.Sales.FindAsync(saleId);
        sale!.SubTotal.Should().Be(20.000m); // 2 * 10
        sale.TaxAmount.Should().Be(3.200m);  // 20 * 0.16
        sale.TotalAmount.Should().Be(23.200m); // 20 + 3.200
    }

    [Fact]
    public async Task AddItemAsync_WithInsufficientStock_Throws()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);

        var request = new AddItemRequest(ProductId, Quantity: 999m, Notes: null, Modifiers: null);

        // Act
        var act = () => service.AddItemAsync(saleId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*الكمية المتاحة غير كافية*");
    }

    [Fact]
    public async Task AddItemAsync_ToNonExistentSale_Throws()
    {
        // Arrange
        var (service, _) = CreateService();
        var request = new AddItemRequest(ProductId, Quantity: 1m, Notes: null, Modifiers: null);

        // Act
        var act = () => service.AddItemAsync(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("البيع غير موجود");
    }

    // ========================================================================
    // RemoveItemAsync — Removing Items & Inventory Release
    // ========================================================================

    [Fact]
    public async Task RemoveItemAsync_RemovesItem_AndReleasesReservation()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 3m, null, null));

        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        var itemId = items[0].Id;

        // Act
        await service.RemoveItemAsync(saleId, itemId);

        // Assert — item removed
        var remainingItems = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        remainingItems.Should().BeEmpty();

        // Assert — inventory released
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.ReservedQuantity.Should().Be(0m);

        // Assert — sale totals reset
        var sale = await context.Sales.FindAsync(saleId);
        sale!.TotalAmount.Should().Be(0);
    }

    // ========================================================================
    // UpdateItemQuantityAsync — Quantity Adjustment
    // ========================================================================

    [Fact]
    public async Task UpdateItemQuantityAsync_IncreasesQuantity_AdjustsReservation()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        var itemId = items[0].Id;

        // Act — increase from 2 to 5
        await service.UpdateItemQuantityAsync(saleId, itemId, 5m);

        // Assert — item quantity updated
        var updatedItem = await context.Set<SaleItem>().FindAsync(itemId);
        updatedItem!.Quantity.Should().Be(5m);

        // Assert — reservation increased by 3
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.ReservedQuantity.Should().Be(5m);

        // Assert — totals recalculated
        var sale = await context.Sales.FindAsync(saleId);
        sale!.TotalAmount.Should().Be(58.000m); // 5*10 + 5*10*0.16 = 50 + 8 = 58
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_ExceedsAvailable_Throws()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        var itemId = items[0].Id;

        // Act — try to increase by more than available (100 stock - 2 reserved = 98 available)
        var act = () => service.UpdateItemQuantityAsync(saleId, itemId, 200m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*الكمية المتاحة غير كافية*");
    }

    // ========================================================================
    // ApplyDiscountAsync — Discount Validation
    // ========================================================================

    [Fact]
    public async Task ApplyDiscountAsync_ValidDiscount_AppliesAndLogs()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        // Act — discount of 2.000 on subtotal 20.000
        await service.ApplyDiscountAsync(new ApplyDiscountRequest(saleId, 2.000m, "Customer loyalty"));

        // Assert — discount applied
        var sale = await context.Sales.FindAsync(saleId);
        sale!.DiscountAmount.Should().Be(2.000m);
        sale.TotalAmount.Should().Be(21.200m); // 20 + 3.200 - 2.000 = 21.200
    }

    // ========================================================================
    // HoldSaleAsync + GetHeldSalesAsync — Hold/Retrieve Flow
    // ========================================================================

    [Fact]
    public async Task HoldSaleAsync_HoldsSale_WithSerializedData()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        // Act
        var heldSaleId = await service.HoldSaleAsync(saleId, "Customer stepped out");

        // Assert — held sale record exists
        var heldSale = await context.HeldSales.FindAsync(heldSaleId);
        heldSale.Should().NotBeNull();
        heldSale!.HoldReason.Should().Be("Customer stepped out");
        heldSale.ShiftId.Should().Be(shiftId);
        heldSale.SerializedData.Should().Contain(saleId.ToString());
        // Product name is serialized with Unicode escapes (\uXXXX) by System.Text.Json
        // instead of literal Arabic characters; verify by checking JSON property names and values
        heldSale.SerializedData.Should().Contain("\"ProductName\"");
        heldSale.SerializedData.Should().Contain("\"LineTotal\":23.200");
        heldSale.SerializedData.Should().Contain("\"TotalAmount\":23.200");

        // Assert — sale status is Held
        var sale = await context.Sales.FindAsync(saleId);
        sale!.Status.Should().Be(SaleStatus.Held);
    }

    [Fact]
    public async Task GetHeldSalesAsync_ReturnsHeldSales_WithParsedAmounts()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId1 = await service.CreateNewSaleAsync(UserId, shiftId);
        var saleId2 = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId1, new AddItemRequest(ProductId, 1m, null, null));
        await service.AddItemAsync(saleId2, new AddItemRequest(ProductId, 3m, null, null));
        await service.HoldSaleAsync(saleId1, "First hold");
        await service.HoldSaleAsync(saleId2, "Second hold");

        // Act
        var heldSales = await service.GetHeldSalesAsync(shiftId);

        // Assert
        heldSales.Should().HaveCount(2);
        heldSales.Should().Contain(h => h.HoldReason == "First hold");
        heldSales.Should().Contain(h => h.HoldReason == "Second hold");
    }

    // ========================================================================
    // GetSaleSummaryAsync + GetSaleItemsAsync — Read Operations
    // ========================================================================

    [Fact]
    public async Task GetSaleSummaryAsync_ReturnsCorrectTotals()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 4m, null, null));

        // Act
        var summary = await service.GetSaleSummaryAsync(saleId);

        // Assert
        summary.SaleId.Should().Be(saleId);
        summary.SubTotal.Should().Be(40.000m);
        summary.TaxAmount.Should().Be(6.400m);
        summary.TotalAmount.Should().Be(46.400m);
        summary.Status.Should().Be("Active");
        summary.InvoiceNumber.Should().StartWith("INV-");
    }

    [Fact]
    public async Task GetSaleItemsAsync_ReturnsItems_WithLineTotals()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 3m, "No sugar", null));

        // Act
        var items = await service.GetSaleItemsAsync(saleId);

        // Assert
        items.Should().HaveCount(1);
        items[0].ProductName.Should().Be("منتج اختباري");
        items[0].Quantity.Should().Be(3m);
        items[0].UnitPrice.Should().Be(10.000m);
        items[0].LineTotal.Should().Be(34.800m); // 30 + 30*0.16 = 34.800
        items[0].Notes.Should().Be("No sugar");
    }

    // ========================================================================
    // ProcessPaymentAsync — Payment Flow (with TestUnitOfWork no-op transactions)
    // ========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_CompletesSale_UpdatesInventoryAndShift()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        // Act
        var result = await service.ProcessPaymentAsync(new PaymentRequest(
            saleId, Amount: 23.200m, PaymentMethod: "Cash", ReferenceNumber: null));

        // Assert — payment succeeded
        result.Success.Should().BeTrue();
        result.ChangeAmount.Should().Be(0m);

        // Assert — sale is completed
        var sale = await context.Sales.FindAsync(saleId);
        sale!.Status.Should().Be(SaleStatus.Completed);
        sale.IsPaid.Should().BeTrue();
        sale.PaidAt.Should().NotBeNull();

        // Assert — inventory deducted (not just reserved)
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.Quantity.Should().Be(98m); // 100 - 2
        inventory.ReservedQuantity.Should().Be(0m);

        // Assert — payment record created
        var payment = await context.Set<Payment>().FirstAsync(p => p.SaleId == saleId);
        payment.Amount.Should().Be(23.200m);
        payment.PaymentMethod.Should().Be(PaymentMethod.Cash);

        // Assert — shift totals updated
        var shift = await context.Shifts.FindAsync(shiftId);
        shift!.TotalSales.Should().Be(23.200m);

        // Assert — inventory movement recorded
        var movements = await context.Set<InventoryMovement>()
            .Where(m => m.ProductId == ProductId && m.MovementType == MovementType.Sale)
            .ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].Quantity.Should().Be(-2m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_InsufficientAmount_ReturnsFailure()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        // Act
        var result = await service.ProcessPaymentAsync(new PaymentRequest(
            saleId, Amount: 10.000m, PaymentMethod: "Cash", null));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ========================================================================
    // CancelSaleAsync — Cancellation Flow
    // ========================================================================

    [Fact]
    public async Task CancelSaleAsync_CancelsActiveSale_ReleasesReservation()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));

        // Act
        var result = await service.CancelSaleAsync(saleId, "Customer request");

        // Assert
        result.Success.Should().BeTrue();

        // Sale status is Cancelled
        var sale = await context.Sales.FindAsync(saleId);
        sale!.Status.Should().Be(SaleStatus.Cancelled);

        // Inventory reservation released
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.ReservedQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task CancelSaleAsync_AlreadyCompleted_ReturnsFailure()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 1m, null, null));
        await service.ProcessPaymentAsync(new PaymentRequest(saleId, 11.600m, "Cash", null));

        // Act — try to cancel a completed sale
        var result = await service.CancelSaleAsync(saleId, "Changed mind");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("لا يمكن إلغاء هذا البيع");
    }

    // ========================================================================
    // ReturnItemsAsync — Return Flow
    // ========================================================================

    [Fact]
    public async Task ReturnItemsAsync_ReturnsItems_RestoresInventory()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 3m, null, null));
        await service.ProcessPaymentAsync(new PaymentRequest(saleId, 34.800m, "Cash", null));

        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        var itemId = items[0].Id;

        // Act — return 1 of the 3 items
        var returnRequest = new List<ReturnItemRequest>
        {
            new(itemId, 1m, "Defective")
        };

        var result = await service.ReturnItemsAsync(saleId, returnRequest, "Customer complaint");

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Contain("10.000"); // 1 * 10 unit price

        // Inventory restored (100 - 3 sold + 1 returned = 98)
        var inventory = await context.Set<InventoryItem>().FirstAsync(i => i.ProductId == ProductId);
        inventory.Quantity.Should().Be(98m);

        // Inventory movement recorded
        var movements = await context.Set<InventoryMovement>()
            .Where(m => m.ProductId == ProductId && m.MovementType == MovementType.Return)
            .ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task ReturnItemsAsync_QuantityExceedsSold_Throws()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId, new AddItemRequest(ProductId, 2m, null, null));
        await service.ProcessPaymentAsync(new PaymentRequest(saleId, 23.200m, "Cash", null));

        var items = await context.Set<SaleItem>().Where(i => i.SaleId == saleId).ToListAsync();
        var itemId = items[0].Id;

        // Act — return 5 items but only 2 were sold
        var returnRequest = new List<ReturnItemRequest>
        {
            new(itemId, 5m, "Too many")
        };

        var act = () => service.ReturnItemsAsync(saleId, returnRequest, "Wrong");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الكمية المرتجعة أكبر من الكمية المباعة");
    }

    // ========================================================================
    // SalesHistoryAsync — Sales History Query
    // ========================================================================

    [Fact]
    public async Task GetSalesHistoryAsync_ReturnsFilteredResults()
    {
        // Arrange
        var (service, context) = CreateService();
        var shiftId = GetShiftId(context);
        var saleId1 = await service.CreateNewSaleAsync(UserId, shiftId);
        var saleId2 = await service.CreateNewSaleAsync(UserId, shiftId);
        await service.AddItemAsync(saleId1, new AddItemRequest(ProductId, 1m, null, null));
        await service.AddItemAsync(saleId2, new AddItemRequest(ProductId, 2m, null, null));

        // Act
        var history = await service.GetSalesHistoryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Assert
        history.Should().HaveCount(2);
        history.Should().Contain(h => h.SaleId == saleId1);
        history.Should().Contain(h => h.SaleId == saleId2);
    }
}
