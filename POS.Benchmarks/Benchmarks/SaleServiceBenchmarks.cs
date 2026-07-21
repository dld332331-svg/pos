using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using POS.Application.DTOs;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;
using POS.Infrastructure.Repositories;

namespace POS.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for SaleService.ProcessPaymentAsync — the critical payment path.
///
/// Measures end-to-end throughput including:
///   - Sale lookup + validation
///   - Payment record creation
///   - Inventory deduction (per item)
///   - Shift total update
///   - Audit logging (mocked, no I/O)
///   - Transaction commit (no-op in InMemory)
///
/// Each invocation creates a fresh sale + items so ProcessPayment always
/// operates on an Active sale (unlike a single-use GlobalSetup approach).
/// The CreateNewSale + AddItem overhead is identical across ItemCount tiers,
/// so relative comparisons between param values are accurate.
///
/// Uses EF Core InMemory with real POSDbContext and Repository{T}
/// for realistic data access patterns (no mocks on IUnitOfWork or DbContext).
/// </summary>
[MemoryDiagnoser]
public class SaleServiceBenchmarks
{
    // Benchmark dimensions
    [Params(1, 5, 25)]
    public int ItemCount { get; set; }

    private SaleService _service = null!;
    private POSDbContext _context = null!;
    private Mock<IAuditService> _auditMock = null!;
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid RegisterId = Guid.NewGuid();

    /// <summary>
    /// Seeds baseline entities once. Sale creation happens per-invocation inside the benchmark.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var dbName = $"POS_Bench_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _context = new POSDbContext(options);
        var unitOfWork = new TestUnitOfWorkForBench(_context);
        _auditMock = new Mock<IAuditService>();
        _auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _service = new SaleService(unitOfWork, _auditMock.Object);

        // Seed baseline data (users, products, inventory, shift)
        SeedBaseline().GetAwaiter().GetResult();
    }

    private async Task SeedBaseline()
    {
        var user = new User
        {
            Id = UserId,
            Username = "benchuser",
            FullName = "Bench User",
            Role = UserRole.Cashier,
            IsActive = true
        };
        _context.Users.Add(user);

        var category = new Category
        {
            Id = CategoryId,
            Name = "Bench Category",
            IsActive = true
        };
        _context.Categories.Add(category);

        var product = new Product
        {
            Id = ProductId,
            Name = "Bench Product",
            ArabicName = "منتج اختباري",
            Sku = "BENCH-001",
            Price = 15.000m,
            Cost = 8.000m,
            TaxRate = 0.16m,
            MinStock = 5m,
            CategoryId = CategoryId,
            Status = ProductStatus.Active
        };
        _context.Products.Add(product);

        // Ample stock for all param values
        var inventory = new InventoryItem
        {
            ProductId = ProductId,
            Name = "Bench Product Inv",
            Quantity = 10000m,
            ReservedQuantity = 0,
            Cost = 8.000m,
            Unit = "piece"
        };
        _context.InventoryItems.Add(inventory);

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
        _context.Shifts.Add(shift);

        var register = new Register
        {
            Id = RegisterId,
            Name = "Bench Register",
            IsActive = true
        };
        _context.Registers.Add(register);

        await _context.SaveChangesAsync();
    }

    private Guid GetShiftId() => _context.Shifts.First().Id;

    [Benchmark]
    [BenchmarkCategory("SaleService", "ProcessPayment")]
    public async Task<PaymentResult> ProcessPayment()
    {
        // Create fresh sale + items for each benchmark invocation.
        // This guarantees ProcessPayment always operates on an Active sale.
        var shiftId = GetShiftId();
        var saleId = await _service.CreateNewSaleAsync(UserId, shiftId);

        for (int i = 0; i < ItemCount; i++)
        {
            await _service.AddItemAsync(saleId,
                new AddItemRequest(ProductId, 1m, Notes: null, Modifiers: null));
        }

        var total = 15.000m * ItemCount * 1.16m;
        return await _service.ProcessPaymentAsync(new PaymentRequest(
            saleId, Amount: total, PaymentMethod: "Cash", ReferenceNumber: null));
    }

    [Benchmark]
    [BenchmarkCategory("SaleService", "CancelSale")]
    public async Task<OperationResult> CancelSale()
    {
        // Create fresh sale + items, then cancel it.
        // Measures: sale lookup, status validation, inventory reservation release,
        // status transition to Cancelled, and audit logging.
        var shiftId = GetShiftId();
        var saleId = await _service.CreateNewSaleAsync(UserId, shiftId);

        for (int i = 0; i < ItemCount; i++)
        {
            await _service.AddItemAsync(saleId,
                new AddItemRequest(ProductId, 1m, Notes: null, Modifiers: null));
        }

        return await _service.CancelSaleAsync(saleId, "Benchmark cancellation");
    }

    [Benchmark]
    [BenchmarkCategory("SaleService", "HoldSale")]
    public async Task<Guid> HoldSale()
    {
        // Create fresh sale + items, then hold it.
        // Measures: sale lookup, status validation, JSON serialization of items,
        // HeldSale record creation, status transition to Held, and save.
        var shiftId = GetShiftId();
        var saleId = await _service.CreateNewSaleAsync(UserId, shiftId);

        for (int i = 0; i < ItemCount; i++)
        {
            await _service.AddItemAsync(saleId,
                new AddItemRequest(ProductId, 1m, Notes: null, Modifiers: null));
        }

        return await _service.HoldSaleAsync(saleId, "Benchmark hold");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }
}

/// <summary>
/// Minimal TestUnitOfWork for benchmarks — same pattern as integration tests.
/// Uses real Repository{T} with EF Core InMemory for realistic data access.
/// </summary>
public sealed class TestUnitOfWorkForBench : IUnitOfWork
{
    private readonly POSDbContext _context;

    public TestUnitOfWorkForBench(POSDbContext context)
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
        UnitOfMeasures = new Repository<UnitOfMeasure>(_context);
        AuditLogs = new SimpleRepository<AuditLog>(_context);
        BackupRecords = new SimpleRepository<BackupRecord>(_context);
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
    public IRepository<UnitOfMeasure> UnitOfMeasures { get; }
    public ISimpleRepository<AuditLog> AuditLogs { get; }
    public ISimpleRepository<BackupRecord> BackupRecords { get; }

    // Transaction methods: no-ops for InMemory
    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task CommitAsync() => Task.CompletedTask;
    public Task RollbackAsync() => Task.CompletedTask;
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public async Task<bool> CanConnectAsync() => await _context.Database.CanConnectAsync();
}
