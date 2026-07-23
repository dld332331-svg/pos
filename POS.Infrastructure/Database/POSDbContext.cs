using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Domain.Entities;

namespace POS.Infrastructure.Database;

public class POSDbContext : DbContext
{
    public POSDbContext(DbContextOptions<POSDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder is null) return;
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    // Users & Auth
    public DbSet<User> Users => Set<User>();

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Modifiers
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();
    public DbSet<ModifierSize> ModifierSizes => Set<ModifierSize>();

    // Restaurant Layout
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();

    // Sales
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleItemModifier> SaleItemModifiers => Set<SaleItemModifier>();
    public DbSet<Payment> Payments => Set<Payment>();

    // CRM & Procurement
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    // Operations
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<WithdrawalDeposit> WithdrawalDeposits => Set<WithdrawalDeposit>();

    // System
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    // Promotions
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<SalePromotion> SalePromotions => Set<SalePromotion>();

    // Inventory Batches
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();

    // Units of Measure
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();

    /// <summary>
    /// Spec §5 (Financial Precision): ALL monetary and quantity values are stored as
    /// DECIMAL(18,3) — Jordanian Dinar with 3 decimal places. This convention applies
    /// to every decimal property in the model (current and future). Explicit
    /// HasColumnType("DECIMAL(18,3)") calls below are kept for documentation and
    /// simply match this convention.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HaveColumnType("DECIMAL(18,3)");
        configurationBuilder.Properties<decimal?>().HaveColumnType("DECIMAL(18,3)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // ============================================================
        // Global: Soft delete query filter for all BaseEntity entities
        // ============================================================
        // Cache the open generic method once to avoid repeated reflection lookups.
        var applyQueryFilterMethod = typeof(POSDbContext).GetMethod(nameof(ApplyQueryFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                // AuditLog is always queryable regardless of IsDeleted
                if (entityType.ClrType != typeof(AuditLog))
                {
                    var method = applyQueryFilterMethod!.MakeGenericMethod(entityType.ClrType);
                    method.Invoke(null, new object[] { modelBuilder });
                }
            }
        }

        // ============================================================
        // User
        // ============================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.Pin).HasMaxLength(20);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // ============================================================
        // Category
        // ============================================================
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.ParentCategory)
                  .WithMany(c => c.SubCategories)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // Product
        // ============================================================
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Sku).HasMaxLength(50);
            entity.Property(e => e.Barcode).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Cost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TaxRate).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.MinStock).HasColumnType("DECIMAL(18,3)");

            entity.HasIndex(e => e.Barcode);
            entity.HasIndex(e => e.Sku);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.KitchenStation)
                  .WithMany(k => k.Products)
                  .HasForeignKey(e => e.KitchenStationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.UnitOfMeasure)
                  .WithMany()
                  .HasForeignKey(e => e.UnitOfMeasureId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // InventoryItem
        // ============================================================
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Cost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ReservedQuantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.MinQuantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.MaxQuantity).HasColumnType("DECIMAL(18,3)");
            entity.HasOne(e => e.Supplier)
                  .WithMany(s => s.InventoryItems)
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // Recipe
        // ============================================================
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Product)
                  .WithOne(p => p.Recipe)
                  .HasForeignKey<Recipe>(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // RecipeIngredient
        // ============================================================
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.HasOne(e => e.Recipe)
                  .WithMany(r => r.Ingredients)
                  .HasForeignKey(e => e.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.InventoryItem)
                  .WithMany(i => i.RecipeIngredients)
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // ModifierGroup
        // ============================================================
        modelBuilder.Entity<ModifierGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
        });

        // ============================================================
        // Modifier
        // ============================================================
        modelBuilder.Entity<Modifier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Price).HasColumnType("DECIMAL(18,3)");
            entity.HasOne(e => e.ModifierGroup)
                  .WithMany(g => g.Modifiers)
                  .HasForeignKey(e => e.ModifierGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // ModifierSize
        // ============================================================
        modelBuilder.Entity<ModifierSize>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Price).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.PriceAdjustment).HasColumnType("DECIMAL(18,3)");
            entity.HasOne(e => e.Modifier)
                  .WithMany(m => m.Sizes)
                  .HasForeignKey(e => e.ModifierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // Room
        // ============================================================
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
        });

        // ============================================================
        // Table
        // ============================================================
        modelBuilder.Entity<Table>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Capacity).HasDefaultValue(4);
            entity.HasOne(e => e.Room)
                  .WithMany(r => r.Tables)
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // KitchenStation
        // ============================================================
        modelBuilder.Entity<KitchenStation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
        });

        // ============================================================
        // Sale
        // ============================================================
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.SubTotal).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TaxAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.DiscountAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.RoundAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.RemainingAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CustomerName).HasMaxLength(200);

            entity.HasIndex(e => e.InvoiceNumber);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sales)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Table)
                  .WithMany(t => t.Sales)
                  .HasForeignKey(e => e.TableId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Sales)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Register)
                  .WithMany(r => r.Sales)
                  .HasForeignKey(e => e.RegisterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Shift)
                  .WithMany(s => s.Sales)
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // SaleItem
        // ============================================================
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.ProductArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.UnitPrice).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Discount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.DiscountAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TaxRate).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TaxAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalPrice).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.LineTotal).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Cost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.Sale)
                  .WithMany(s => s.SaleItems)
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.KitchenStation)
                  .WithMany()
                  .HasForeignKey(e => e.KitchenStationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.UnitOfMeasure)
                  .WithMany()
                  .HasForeignKey(e => e.UnitOfMeasureId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // SaleItemModifier
        // ============================================================
        modelBuilder.Entity<SaleItemModifier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModifierName).HasMaxLength(150);
            entity.Property(e => e.ModifierArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.SizeName).HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.AdditionalPrice).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");

            entity.HasOne(e => e.SaleItem)
                  .WithMany(si => si.Modifiers)
                  .HasForeignKey(e => e.SaleItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // Payment
        // ============================================================
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TipAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.CardLast4).HasMaxLength(4);

            entity.HasOne(e => e.Sale)
                  .WithMany(s => s.Payments)
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // Customer
        // ============================================================
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Balance).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalPurchases).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
            entity.HasIndex(e => e.Phone);
        });

        // ============================================================
        // Supplier
        // ============================================================
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.ContactPerson).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Balance).HasColumnType("DECIMAL(18,3)");
        });

        // ============================================================
        // PurchaseOrder
        // ============================================================
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.PaidAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.Supplier)
                  .WithMany(s => s.PurchaseOrders)
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // PurchaseOrderItem
        // ============================================================
        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(200);
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.UnitCost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalCost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ReceivedQuantity).HasColumnType("DECIMAL(18,3)");

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(po => po.Items)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.InventoryItem)
                  .WithMany()
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // InventoryMovement
        // ============================================================
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.BeforeQuantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.AfterQuantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.UnitCost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalCost).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => e.ProductId);

            entity.HasOne(e => e.InventoryItem)
                  .WithMany(i => i.InventoryMovements)
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Sale)
                  .WithMany()
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.InventoryBatch)
                  .WithMany()
                  .HasForeignKey(e => e.InventoryBatchId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // Shift
        // ============================================================
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OpeningCash).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ClosingCash).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ExpectedCash).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.CashSales).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.CardSales).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.OtherSales).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalSales).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalReturns).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalExpenses).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalWithdrawals).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalDeposits).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Difference).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ActualCash).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Variance).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Shifts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Register)
                  .WithMany(r => r.Shifts)
                  .HasForeignKey(e => e.RegisterId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // Expense
        // ============================================================
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ArabicDescription).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Amount).HasColumnType("DECIMAL(18,3)");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Shift)
                  .WithMany(s => s.Expenses)
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // WithdrawalDeposit
        // ============================================================
        modelBuilder.Entity<WithdrawalDeposit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ArabicReason).HasColumnType("NVARCHAR(200)");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Shift)
                  .WithMany(s => s.WithdrawalDeposits)
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // Printer
        // ============================================================
        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.Port).HasDefaultValue(9100);
            entity.Property(e => e.ConnectionString).HasMaxLength(500);
        });

        // ============================================================
        // Register
        // ============================================================
        modelBuilder.Entity<Register>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.CurrentBalance).HasColumnType("DECIMAL(18,3)");
        });

        // ============================================================
        // AuditLog — No soft delete filter (always queryable)
        // ============================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActionType);
            entity.Property(e => e.EntityName).HasMaxLength(200);
            entity.Property(e => e.EntityId);
            entity.Property(e => e.BeforeValue).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.AfterValue).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.IPAddress).HasMaxLength(50);

            entity.HasIndex(e => e.Timestamp);
        });

        // ============================================================
        // Setting
        // ============================================================
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Value).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // ============================================================
        // BackupRecord
        // ============================================================
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.FileSize);
        });

        // ============================================================
        // UnitOfMeasure
        // ============================================================
        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ArabicName).HasColumnType("NVARCHAR(100)");
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.ArabicSymbol).HasColumnType("NVARCHAR(100)");
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ConversionFactor).HasColumnType("DECIMAL(18,6)");
            entity.HasIndex(e => e.Category);
        });

        // ============================================================
        // HeldSale
        // ============================================================
        modelBuilder.Entity<HeldSale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerializedData).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Table)
                  .WithMany()
                  .HasForeignKey(e => e.TableId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Shift)
                  .WithMany()
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // Return
        // ============================================================
        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReturnNumber).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.TotalAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.RefundAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TaxAmount).HasColumnType("DECIMAL(18,3)");

            entity.HasOne(e => e.Sale)
                  .WithMany()
                  .HasForeignKey(e => e.OriginalSaleId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // ReturnItem
        // ============================================================
        modelBuilder.Entity<ReturnItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.ProductArabicName).HasColumnType("NVARCHAR(200)");
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.UnitPrice).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.TotalPrice).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.ReturnAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(e => e.Return)
                  .WithMany(r => r.Items)
                  .HasForeignKey(e => e.ReturnId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // InventoryBatch
        // ============================================================
        modelBuilder.Entity<InventoryBatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Quantity).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.UnitCost).HasColumnType("DECIMAL(18,3)");
            entity.HasIndex(e => new { e.InventoryItemId, e.BatchNumber });
            entity.HasOne(e => e.InventoryItem)
                  .WithMany(i => i.Batches)
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // Promotion
        // ============================================================
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Value).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.MinPurchaseAmount).HasColumnType("DECIMAL(18,3)").IsRequired(false);
            entity.Property(e => e.ApplicableProductIdsJson).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ApplicableCategoryIdsJson).HasColumnType("NVARCHAR(MAX)");
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.StartDate, e.EndDate });
        });

        // ============================================================
        // SalePromotion
        // ============================================================
        modelBuilder.Entity<SalePromotion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiscountAmount).HasColumnType("DECIMAL(18,3)");
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.Sale)
                  .WithMany(s => s.AppliedPromotions)
                  .HasForeignKey(e => e.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Promotion)
                  .WithMany()
                  .HasForeignKey(e => e.PromotionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ApplyQueryFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }
}