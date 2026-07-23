using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;

namespace POS.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly POSDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    // Catalog
    public IRepository<User> Users { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Product> Products { get; }
    public IRepository<InventoryItem> InventoryItems { get; }
    public IRepository<Recipe> Recipes { get; }
    public IRepository<RecipeIngredient> RecipeIngredients { get; }

    // Modifiers
    public IRepository<ModifierGroup> ModifierGroups { get; }
    public IRepository<Modifier> Modifiers { get; }
    public IRepository<ModifierSize> ModifierSizes { get; }

    // Restaurant Layout
    public IRepository<Table> Tables { get; }
    public IRepository<Room> Rooms { get; }
    public IRepository<KitchenStation> KitchenStations { get; }

    // Sales
    public IRepository<Sale> Sales { get; }
    public IRepository<SaleItem> SaleItems { get; }
    public IRepository<SaleItemModifier> SaleItemModifiers { get; }
    public IRepository<Payment> Payments { get; }

    // CRM & Procurement
    public IRepository<Customer> Customers { get; }
    public IRepository<Supplier> Suppliers { get; }
    public IRepository<PurchaseOrder> PurchaseOrders { get; }
    public IRepository<PurchaseOrderItem> PurchaseOrderItems { get; }

    // Operations
    public IRepository<InventoryMovement> InventoryMovements { get; }
    public IRepository<Shift> Shifts { get; }
    public IRepository<Expense> Expenses { get; }
    public IRepository<WithdrawalDeposit> WithdrawalDeposits { get; }

    // System
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

    // Special (non-BaseEntity)
    public ISimpleRepository<AuditLog> AuditLogs { get; }
    public ISimpleRepository<BackupRecord> BackupRecords { get; }

    public POSDbContext Context => _context;

    public UnitOfWork(POSDbContext context)
    {
        _context = context;

        // Catalog
        Users = new Repository<User>(_context);
        Categories = new Repository<Category>(_context);
        Products = new Repository<Product>(_context);
        InventoryItems = new Repository<InventoryItem>(_context);
        Recipes = new Repository<Recipe>(_context);
        RecipeIngredients = new Repository<RecipeIngredient>(_context);

        // Modifiers
        ModifierGroups = new Repository<ModifierGroup>(_context);
        Modifiers = new Repository<Modifier>(_context);
        ModifierSizes = new Repository<ModifierSize>(_context);

        // Restaurant Layout
        Tables = new Repository<Table>(_context);
        Rooms = new Repository<Room>(_context);
        KitchenStations = new Repository<KitchenStation>(_context);

        // Sales
        Sales = new Repository<Sale>(_context);
        SaleItems = new Repository<SaleItem>(_context);
        SaleItemModifiers = new Repository<SaleItemModifier>(_context);
        Payments = new Repository<Payment>(_context);

        // CRM & Procurement
        Customers = new Repository<Customer>(_context);
        Suppliers = new Repository<Supplier>(_context);
        PurchaseOrders = new Repository<PurchaseOrder>(_context);
        PurchaseOrderItems = new Repository<PurchaseOrderItem>(_context);

        // Operations
        InventoryMovements = new Repository<InventoryMovement>(_context);
        Shifts = new Repository<Shift>(_context);
        Expenses = new Repository<Expense>(_context);
        WithdrawalDeposits = new Repository<WithdrawalDeposit>(_context);

        // System
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

        // Special (non-BaseEntity)
        AuditLogs = new SimpleRepository<AuditLog>(_context);
        BackupRecords = new SimpleRepository<BackupRecord>(_context);
    }

    public async Task BeginTransactionAsync()
    {
        if (_currentTransaction != null)
            return;

        // In-memory provider does not support transactions; we still enter the
        // transaction scope so that callers can commit/rollback uniformly.
        if (string.Equals(_context.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // Use a lightweight sentinel transaction so CommitAsync/RollbackAsync know we are in scope.
            _currentTransaction = new InMemoryDbContextTransaction();
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        try
        {
            // Persist any pending changes (including those added after an explicit
            // SaveChangesAsync call, such as audit log entries). This ensures the
            // transaction is always committed with the complete unit of work.
            await _context.SaveChangesAsync();

            if (_currentTransaction is InMemoryDbContextTransaction inMemoryTx)
            {
                await inMemoryTx.CommitAsync();
            }
            else if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync();
            }
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackAsync()
    {
        try
        {
            if (_currentTransaction is InMemoryDbContextTransaction inMemoryTx)
            {
                await inMemoryTx.RollbackAsync();
            }
            else if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync();
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("A concurrency error occurred while saving changes.", ex);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("An error occurred while saving changes to the database.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An unexpected error occurred while saving changes.", ex);
        }
    }

    public void Dispose()
    {
        if (_currentTransaction != null)
        {
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
        _context.Dispose();
    }

    /// <summary>
    /// Lightweight transaction implementation for the EF Core InMemory provider,
    /// which does not support real transactions. It keeps the UnitOfWork contract
    /// uniform across unit/integration tests and production code.
    /// </summary>
    private sealed class InMemoryDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool IsCommitted { get; private set; }
        public bool IsRolledBack { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            IsCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            IsRolledBack = true;
            return Task.CompletedTask;
        }

        public void Commit()
        {
            IsCommitted = true;
        }

        public void Rollback()
        {
            IsRolledBack = true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}