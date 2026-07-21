using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Simple repository for entities that do NOT inherit from <see cref="BaseEntity"/>.
/// Used for AuditLog and BackupRecord which have fixed schemas with no soft-delete.
/// </summary>
public interface ISimpleRepository<T> where T : class
{
    /// <summary>Gets an entity by its ID.</summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>Gets all entities.</summary>
    Task<IReadOnlyList<T>> GetAllAsync();

    /// <summary>Adds a new entity.</summary>
    Task AddAsync(T entity);

    /// <summary>Deletes an entity.</summary>
    Task DeleteAsync(T entity);
}

/// <summary>
/// Unit of Work pattern that coordinates repository operations and transactions.
/// Provides a single point of access to all repositories in the system.
/// </summary>
public interface IUnitOfWork
{
    // --- Transaction Management ---

    /// <summary>Starts a new database transaction.</summary>
    Task BeginTransactionAsync();

    /// <summary>Commits the current transaction.</summary>
    Task CommitAsync();

    /// <summary>Rolls back the current transaction.</summary>
    Task RollbackAsync();

    /// <summary>Saves all pending changes to the database.</summary>
    /// <returns>The number of entities affected.</returns>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Checks whether the underlying database is reachable (spec §13: AUTH-001
    /// database status indicator). Returns false instead of throwing.
    /// </summary>
    Task<bool> CanConnectAsync();

    // --- Repository Properties (BaseEntity-derived) ---

    IRepository<User> Users { get; }
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    IRepository<InventoryItem> InventoryItems { get; }
    IRepository<Recipe> Recipes { get; }
    IRepository<RecipeIngredient> RecipeIngredients { get; }
    IRepository<ModifierGroup> ModifierGroups { get; }
    IRepository<Modifier> Modifiers { get; }
    IRepository<ModifierSize> ModifierSizes { get; }
    IRepository<Table> Tables { get; }
    IRepository<Room> Rooms { get; }
    IRepository<KitchenStation> KitchenStations { get; }
    IRepository<Sale> Sales { get; }
    IRepository<SaleItem> SaleItems { get; }
    IRepository<SaleItemModifier> SaleItemModifiers { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Supplier> Suppliers { get; }
    IRepository<PurchaseOrder> PurchaseOrders { get; }
    IRepository<PurchaseOrderItem> PurchaseOrderItems { get; }
    IRepository<InventoryMovement> InventoryMovements { get; }
    IRepository<Shift> Shifts { get; }
    IRepository<Expense> Expenses { get; }
    IRepository<WithdrawalDeposit> WithdrawalDeposits { get; }
    IRepository<Printer> Printers { get; }
    IRepository<Register> Registers { get; }
    IRepository<Setting> Settings { get; }
    IRepository<HeldSale> HeldSales { get; }
    IRepository<Return> Returns { get; }
    IRepository<ReturnItem> ReturnItems { get; }
    IRepository<Promotion> Promotions { get; }
    IRepository<SalePromotion> SalePromotions { get; }
    IRepository<InventoryBatch> InventoryBatches { get; }
    IRepository<UnitOfMeasure> UnitOfMeasures { get; }

    // --- Special Repository Properties (non-BaseEntity) ---

    /// <summary>AuditLog repository (append-only, no soft-delete, no modification).</summary>
    ISimpleRepository<AuditLog> AuditLogs { get; }

    /// <summary>BackupRecord repository (append-only, no soft-delete).</summary>
    ISimpleRepository<BackupRecord> BackupRecords { get; }
}