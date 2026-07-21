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
/// Integration tests for PromotionService, PurchaseOrderService, RecipeService,
/// SupplierService, KitchenOrderService, and PrinterManagementService.
///
/// Uses EF Core InMemory with real POSDbContext + Repository{T} instances (via TestUnitOfWork),
/// providing real OnModelCreating behavior, soft-delete filters, and FK constraints
/// without requiring a SQL Server instance.
/// </summary>
public sealed class ServiceIntegrationTests
{
    // ========================================================================
    // PROMOTION SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class PromotionServiceIntegrationTests
    {
        [Fact]
        public async Task CreatePromotion_AndGetById_ReturnsPersistedPromotion()
        {
            // Arrange
            var dbName = $"POS_PromoTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var service = new PromotionService(unitOfWork, auditMock.Object);

            var request = new CreatePromotionRequest(
                Name: "خصم 15%", Description: "خصم موسمي",
                Type: "Percentage", Value: 15m,
                StartDate: DateTime.UtcNow, EndDate: DateTime.UtcNow.AddDays(7));

            // Act
            var created = await service.CreateAsync(request);
            var fetched = await service.GetByIdAsync(created.Id);

            // Assert — persisted and retrievable
            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("خصم 15%");
            fetched.Value.Should().Be(15m);
            fetched.Type.Should().Be("Percentage");
            fetched.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task ApplyPromotion_SaleWithItems_CalculatesAndPersistsDiscount()
        {
            // Arrange
            var dbName = $"POS_PromoApply_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);

            // Seed: user, category, product, inventory, shift, register
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var registerId = Guid.NewGuid();

            context.Users.Add(new User { Id = userId, Username = "u1", FullName = "User", Role = UserRole.Cashier, IsActive = true });
            context.Categories.Add(new Category { Id = categoryId, Name = "Cat", IsActive = true });
            context.Products.Add(new Product { Id = productId, Name = "Prod", ArabicName = "منتج", Sku = "P1", Price = 50.000m, Cost = 20m, TaxRate = 0.16m, MinStock = 1, CategoryId = categoryId, Status = ProductStatus.Active });
            context.InventoryItems.Add(new InventoryItem { Name = "Inv", Quantity = 100m, ProductId = productId, Unit = "pc", Cost = 20m });
            context.Registers.Add(new Register { Id = registerId, Name = "Reg1", IsActive = true });
            context.Shifts.Add(new Shift { Id = shiftId, ShiftNumber = 1, UserId = userId, RegisterId = registerId, OpeningCash = 500m, Status = ShiftStatus.Open, OpenedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var saleService = new SaleService(unitOfWork, auditMock.Object);
            var promoService = new PromotionService(unitOfWork, auditMock.Object);

            // Create sale with 2 items
            var saleId = await saleService.CreateNewSaleAsync(userId, shiftId);
            await saleService.AddItemAsync(saleId, new AddItemRequest(productId, 2m, null, null));

            // Create promotion: 10% off
            var promo = await promoService.CreateAsync(new CreatePromotionRequest(
                Name: "10% Off", Description: null,
                Type: "Percentage", Value: 10m,
                StartDate: DateTime.UtcNow.AddDays(-1), EndDate: DateTime.UtcNow.AddDays(1)));

            var items = await saleService.GetSaleItemsAsync(saleId);

            // Act — apply promotion
            var result = await promoService.ApplyPromotionAsync(saleId, promo.Id, items);

            // Assert — discount calculated and persisted
            result.Should().NotBeNull();
            result!.DiscountAmount.Should().Be(10.000m); // 10% of (2 * 50 = 100)

            // Sale was updated in the database
            var sale = await context.Sales.FindAsync(saleId);
            sale!.DiscountAmount.Should().Be(10.000m);
            sale.TotalAmount.Should().Be(106.000m); // 100 + 16 - 10

            // SalePromotion record persisted
            var appliedPromos = await context.Set<SalePromotion>().Where(sp => sp.SaleId == saleId).ToListAsync();
            appliedPromos.Should().HaveCount(1);
            appliedPromos[0].PromotionId.Should().Be(promo.Id);
            appliedPromos[0].DiscountAmount.Should().Be(10.000m);
        }

        [Fact]
        public async Task GetEligiblePromotionsAsync_ActivePromotions_ReturnsOnlyEligible()
        {
            // Arrange
            var dbName = $"POS_PromoElig_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var service = new PromotionService(unitOfWork, auditMock.Object);

            // Seed: active promotion, expired promotion, inactive promotion, future promotion
            await service.CreateAsync(new CreatePromotionRequest("Active 10%", null, "Percentage", 10m,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
            await service.CreateAsync(new CreatePromotionRequest("Expired", null, "Percentage", 5m,
                DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-1)));
            await service.CreateAsync(new CreatePromotionRequest("Inactive", null, "FixedAmount", 20m,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

            // Mark third promotion as inactive
            var allPromos = await context.Promotions.ToListAsync();
            allPromos[2].IsActive = false;
            await context.SaveChangesAsync();

            var items = new List<SaleItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Product", 2m, 50.000m, 0m, 0.16m, 0m, 0m, 0m, null, null)
            };

            // Act
            var eligible = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

            // Assert — only the active 10% promotion qualifies
            eligible.Should().HaveCount(1);
            eligible[0].Name.Should().Be("Active 10%");
            eligible[0].DiscountAmount.Should().Be(10.000m); // 10% of 100
        }
    }

    // ========================================================================
    // PURCHASE ORDER SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class PurchaseOrderServiceIntegrationTests
    {
        [Fact]
        public async Task CreateAndReceivePurchaseOrder_UpdatesInventory()
        {
            // Arrange
            var dbName = $"POS_POTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);

            // Seed: supplier, inventory item
            var supplierId = Guid.NewGuid();
            var inventoryItemId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            context.Suppliers.Add(new Supplier { Id = supplierId, Name = "المورد", IsActive = true });
            context.InventoryItems.Add(new InventoryItem
            {
                Id = inventoryItemId,
                Name = "مادة خام",
                Quantity = 50m,
                Cost = 5.000m,
                ProductId = productId,
                Unit = "kg"
            });
            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Product",
                ArabicName = "منتج",
                Sku = "SKU",
                Price = 15.000m,
                Cost = 5.000m,
                CategoryId = Guid.NewGuid(),
                Status = ProductStatus.Active
            });
            await context.SaveChangesAsync();

            var purchaseService = new PurchaseOrderService(unitOfWork, auditMock.Object, new InventoryService(unitOfWork, auditMock.Object));
            var items = new List<PurchaseOrderItemDto>
            {
                new(inventoryItemId, "مادة خام", 10m, 5.500m, 0m, 0m)
            };

            // Act — create PO
            var po = await purchaseService.CreatePurchaseOrderAsync(supplierId, userId, items, "ملاحظات");

            // Assert — PO created with correct data
            po.OrderNumber.Should().Be("PO-001");
            po.SupplierName.Should().Be("المورد");
            po.Status.Should().Be("Pending");
            po.TotalAmount.Should().Be(55.000m); // 10 * 5.500

            // Act — receive the PO
            var receiveResult = await purchaseService.ReceivePurchaseOrderAsync(po.Id, userId);

            // Assert — PO status updated
            receiveResult.Success.Should().BeTrue();

            // Inventory updated (50 + 10 = 60)
            var inventory = await context.InventoryItems.FindAsync(inventoryItemId);
            inventory!.Quantity.Should().Be(60m);

            // PO status changed in database
            var purchaseOrder = await context.PurchaseOrders.FindAsync(po.Id);
            purchaseOrder!.Status.Should().Be("Received");
        }

        [Fact]
        public async Task CreatePurchaseOrder_SameOrderNumberUsed_Sequential()
        {
            // Arrange
            var dbName = $"POS_POSeq_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var supplierId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            context.Suppliers.Add(new Supplier { Id = supplierId, Name = "S", IsActive = true });
            context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Name = "Item", Quantity = 10m, Cost = 1m, ProductId = Guid.NewGuid(), Unit = "pc" });
            await context.SaveChangesAsync();

            var service = new PurchaseOrderService(unitOfWork, auditMock.Object, new InventoryService(unitOfWork, auditMock.Object));
            var items = new List<PurchaseOrderItemDto> { new(context.InventoryItems.First().Id, "Item", 1m, 10m, 0m, 0m) };

            // Act
            var po1 = await service.CreatePurchaseOrderAsync(supplierId, userId, items, null);
            var po2 = await service.CreatePurchaseOrderAsync(supplierId, userId, items, null);
            var po3 = await service.CreatePurchaseOrderAsync(supplierId, userId, items, null);

            // Assert — sequential order numbers
            po1.OrderNumber.Should().Be("PO-001");
            po2.OrderNumber.Should().Be("PO-002");
            po3.OrderNumber.Should().Be("PO-003");
        }

        [Fact]
        public async Task UpdateStatus_TransitionsCorrectly()
        {
            // Arrange
            var dbName = $"POS_POStatus_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var supplierId = Guid.NewGuid();
            var inventoryItemId = Guid.NewGuid();

            context.Suppliers.Add(new Supplier { Id = supplierId, Name = "S", IsActive = true });
            context.InventoryItems.Add(new InventoryItem { Id = inventoryItemId, Name = "Item", Quantity = 10m, Cost = 5m, ProductId = Guid.NewGuid(), Unit = "pc" });
            await context.SaveChangesAsync();

            var service = new PurchaseOrderService(unitOfWork, auditMock.Object, new InventoryService(unitOfWork, auditMock.Object));
            var po = await service.CreatePurchaseOrderAsync(supplierId, Guid.NewGuid(),
                new List<PurchaseOrderItemDto> { new(inventoryItemId, "I", 1m, 1m, 0m, 0m) }, null);

            // Act — cancel a pending PO
            var result = await service.UpdatePurchaseOrderStatusAsync(po.Id, "Cancelled");

            // Assert
            result.Success.Should().BeTrue();

            var updated = await service.GetPurchaseOrderAsync(po.Id);
            updated!.Status.Should().Be("Cancelled");
        }
    }

    // ========================================================================
    // RECIPE SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class RecipeServiceIntegrationTests
    {
        [Fact]
        public async Task SaveAndGetRecipe_PersistsIngredientsAndCalculatesCost()
        {
            // Arrange
            var dbName = $"POS_RecipeTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);

            var productId = Guid.NewGuid();
            var invItemId1 = Guid.NewGuid();
            var invItemId2 = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            context.Categories.Add(new Category { Id = categoryId, Name = "Cat", IsActive = true });
            context.Products.Add(new Product { Id = productId, Name = "Burger", ArabicName = "برغر", Sku = "B001", Price = 25m, Cost = 8m, TaxRate = 0.16m, MinStock = 1, CategoryId = categoryId, Status = ProductStatus.Active });
            context.InventoryItems.Add(new InventoryItem { Id = invItemId1, Name = "لحم", Quantity = 50m, Cost = 15.000m, ProductId = productId, Unit = "kg" });
            context.InventoryItems.Add(new InventoryItem { Id = invItemId2, Name = "خبز", Quantity = 100m, Cost = 2.000m, ProductId = productId, Unit = "piece" });
            await context.SaveChangesAsync();

            var service = new RecipeService(unitOfWork, auditMock.Object);

            var ingredients = new List<RecipeIngredientDto>
            {
                new(invItemId1, "لحم", 0.2m, "kg"),    // 0.2 * 15.000 = 3.000
                new(invItemId2, "خبز", 1m, "piece")    // 1 * 2.000 = 2.000
            };

            // Act — create recipe
            var recipe = await service.SaveRecipeAsync(productId, "وصفة البرغر", "تعليمات", ingredients);

            // Assert — recipe persisted
            recipe.Name.Should().Be("وصفة البرغر");
            recipe.Ingredients.Should().HaveCount(2);
            recipe.TotalCost.Should().Be(5.000m); // 3.000 + 2.000

            // Act — fetch by product
            var fetched = await service.GetRecipeByProductAsync(productId);

            // Assert — retrievable
            fetched.Should().NotBeNull();
            fetched!.TotalCost.Should().Be(5.000m);

            // Act — calculate cost separately
            var cost = await service.CalculateRecipeCostAsync(recipe.Id);
            cost.Should().Be(5.000m);
        }

        [Fact]
        public async Task UpdateRecipe_ReplacesIngredients()
        {
            // Arrange
            var dbName = $"POS_RecipeUpd_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);

            var productId = Guid.NewGuid();
            var invItemId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            context.Categories.Add(new Category { Id = categoryId, Name = "Cat", IsActive = true });
            context.Products.Add(new Product { Id = productId, Name = "P", ArabicName = "منتج", Sku = "S", Price = 10m, Cost = 3m, TaxRate = 0.16m, MinStock = 1, CategoryId = categoryId, Status = ProductStatus.Active });
            context.InventoryItems.Add(new InventoryItem { Id = invItemId, Name = "المكون", Quantity = 50m, Cost = 8.000m, ProductId = productId, Unit = "kg" });
            await context.SaveChangesAsync();

            var service = new RecipeService(unitOfWork, auditMock.Object);

            // Create with 1 ingredient
            var recipe = await service.SaveRecipeAsync(productId, "Original", null,
                new List<RecipeIngredientDto> { new(invItemId, "المكون", 2m, "kg") });
            recipe.Ingredients.Should().HaveCount(1);

            // Act — update with same ingredient different quantity
            var updated = await service.SaveRecipeAsync(productId, "Updated", "New instructions",
                new List<RecipeIngredientDto> { new(invItemId, "المكون", 5m, "kg") });

            // Assert — ingredient quantity updated
            updated.Name.Should().Be("Updated");
            updated.Instructions.Should().Be("New instructions");
            updated.Ingredients.Should().HaveCount(1);
            updated.Ingredients[0].Quantity.Should().Be(5m);
            updated.TotalCost.Should().Be(40.000m); // 5 * 8.000

            // Old ingredients were removed, only 1 remains
            var ingredientCount = await context.RecipeIngredients.CountAsync();
            ingredientCount.Should().Be(1);
        }

        [Fact]
        public async Task DeleteRecipe_RemovesRecipeAndIngredients()
        {
            // Arrange
            var dbName = $"POS_RecipeDel_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var productId = Guid.NewGuid();
            var invItemId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            context.Categories.Add(new Category { Id = categoryId, Name = "Cat", IsActive = true });
            context.Products.Add(new Product { Id = productId, Name = "P", ArabicName = "منتج", Sku = "S", Price = 10m, Cost = 3m, TaxRate = 0.16m, MinStock = 1, CategoryId = categoryId, Status = ProductStatus.Active });
            context.InventoryItems.Add(new InventoryItem { Id = invItemId, Name = "Ing", Quantity = 50m, Cost = 5m, ProductId = productId, Unit = "kg" });
            await context.SaveChangesAsync();

            var service = new RecipeService(unitOfWork, auditMock.Object);
            var recipe = await service.SaveRecipeAsync(productId, "To Delete", null,
                new List<RecipeIngredientDto> { new(invItemId, "Ing", 1m, "kg") });

            // Act
            await service.DeleteRecipeAsync(recipe.Id);

            // Assert — recipe deleted (use FirstOrDefaultAsync to apply soft-delete query filter;
            // FindAsync bypasses global filters and may return the soft-deleted entity)
            var recipeExists = await context.Recipes.FirstOrDefaultAsync(r => r.Id == recipe.Id);
            recipeExists.Should().BeNull();

            // Ingredients cascade deleted
            var ingredientCount = await context.RecipeIngredients.CountAsync();
            ingredientCount.Should().Be(0);

            // Product still exists (recipe was deleted, not product)
            var productStillExists = await context.Products.FindAsync(productId);
            productStillExists.Should().NotBeNull();
        }
    }

    // ========================================================================
    // SUPPLIER SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class SupplierServiceIntegrationTests
    {
        [Fact]
        public async Task CreateAndGetSuppliers_ReturnsOrderedList()
        {
            // Arrange
            var dbName = $"POS_SuppTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var service = new SupplierService(unitOfWork, auditMock.Object);

            // Act — create 2 suppliers
            var supplier2 = await service.CreateSupplierAsync("مورد ب", "جهة اتصال 2", "0792222222", "b@test.com", "عمان");
            var supplier1 = await service.CreateSupplierAsync("مورد أ", "جهة اتصال 1", "0791111111", "a@test.com", "إربد");

            // Act — get all
            var all = await service.GetSuppliersAsync();

            // Assert — ordered by name
            all.Should().HaveCount(2);
            all[0].Name.Should().Be("مورد أ");
            all[1].Name.Should().Be("مورد ب");

            // Act — search
            var searchResults = await service.GetSuppliersAsync(search: "0791111");
            searchResults.Should().HaveCount(1);
            searchResults[0].Name.Should().Be("مورد أ");

            // Assert — DTO fields mapped
            supplier1.Name.Should().Be("مورد أ");
            supplier1.Phone.Should().Be("0791111111");
            supplier1.Email.Should().Be("a@test.com");
            supplier1.Balance.Should().Be(0m);
            supplier1.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateSupplier_ChangesPersisted()
        {
            // Arrange
            var dbName = $"POS_SuppUpd_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var service = new SupplierService(unitOfWork, auditMock.Object);

            var created = await service.CreateSupplierAsync("Old Name", "Old Contact", "0790000000", "old@test.com", "Old Address");

            // Act
            var updated = await service.UpdateSupplierAsync(created.Id, "New Name", "New Contact", "0799999999", "new@test.com", "New Address");

            // Assert
            updated.Name.Should().Be("New Name");
            updated.ContactPerson.Should().Be("New Contact");
            updated.Phone.Should().Be("0799999999");
            updated.Email.Should().Be("new@test.com");
            updated.Address.Should().Be("New Address");

            // Verify via fresh query
            var all = await service.GetSuppliersAsync();
            all.Should().HaveCount(1);
            all[0].Name.Should().Be("New Name");
        }

        [Fact]
        public async Task DuplicateSupplierName_Throws()
        {
            // Arrange
            var dbName = $"POS_SuppDup_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            var unitOfWork = new TestUnitOfWork(context);
            var service = new SupplierService(unitOfWork, auditMock.Object);
            await service.CreateSupplierAsync("Same Name", null, null, null, null);

            // Act
            var act = () => service.CreateSupplierAsync("Same Name", null, null, null, null);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("يوجد مورد آخر بنفس الاسم");
        }
    }

    // ========================================================================
    // KITCHEN ORDER SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class KitchenOrderServiceIntegrationTests
    {
        [Fact]
        public async Task GetPendingOrdersAsync_ActiveSaleWithStation_ReturnsOrder()
        {
            // Arrange
            var dbName = $"POS_KitchenTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);

            var unitOfWork = new TestUnitOfWork(context);

            // Seed: sale with kitchen station items
            var stationId = Guid.NewGuid();
            context.KitchenStations.Add(new KitchenStation { Id = stationId, Name = "مطبخ اللحوم", IsActive = true });

            var tableId = Guid.NewGuid();
            context.Tables.Add(new Table { Id = tableId, Name = "5", RoomId = Guid.NewGuid(), Capacity = 4 });

            var saleId = Guid.NewGuid();
            context.Sales.Add(new Sale
            {
                Id = saleId,
                InvoiceNumber = "INV-KIT-001",
                UserId = Guid.NewGuid(),
                ShiftId = Guid.NewGuid(),
                OrderType = OrderType.DineIn,
                TableId = tableId,
                Status = SaleStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                SubTotal = 50m,
                TotalAmount = 58m,
                IsPaid = false
            });

            context.SaleItems.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = Guid.NewGuid(),
                ProductName = "ستيك",
                ProductArabicName = "ستيك",
                KitchenStationId = stationId,
                Quantity = 2m,
                UnitPrice = 25.000m,
                LineTotal = 50.000m
            });

            context.SaleItems.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ProductId = Guid.NewGuid(),
                ProductName = "مشروب",
                ProductArabicName = "مشروب",
                KitchenStationId = null, // No station — not a kitchen item
                Quantity = 1m,
                UnitPrice = 5.000m,
                LineTotal = 5.000m
            });
            await context.SaveChangesAsync();

            var service = new KitchenOrderService(unitOfWork);

            // Act
            var orders = await service.GetPendingOrdersAsync();

            // Assert
            orders.Should().HaveCount(1); // Only the kitchen item (drink filtered out)
            orders[0].OrderNumber.Should().Be("INV-KIT-001");
            orders[0].Station.Should().Be("مطبخ اللحوم");
            orders[0].TableOrType.Should().Be("طاولة 5");
            orders[0].Items.Should().HaveCount(1);
            orders[0].Items[0].Name.Should().Be("ستيك");
            orders[0].Items[0].Quantity.Should().Be(2m);
            orders[0].IsPriority.Should().BeFalse(); // Only 10 min old
        }

        [Fact]
        public async Task GetPendingOrdersAsync_OlderThan30Minutes_FlagsAsPriority()
        {
            // Arrange
            var dbName = $"POS_KitchenPri_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);

            var unitOfWork = new TestUnitOfWork(context);
            var stationId = Guid.NewGuid();
            context.KitchenStations.Add(new KitchenStation { Id = stationId, Name = "المطبخ", IsActive = true });

            var saleId = Guid.NewGuid();
            context.Sales.Add(new Sale
            {
                Id = saleId, InvoiceNumber = "INV-PRI", UserId = Guid.NewGuid(), ShiftId = Guid.NewGuid(),
                OrderType = OrderType.Takeaway, Status = SaleStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMinutes(-45), // Over 30 min
                SubTotal = 30m, TotalAmount = 34.800m, IsPaid = false
            });
            context.SaleItems.Add(new SaleItem
            {
                Id = Guid.NewGuid(), SaleId = saleId, ProductId = Guid.NewGuid(),
                ProductName = "بيتزا", ProductArabicName = "بيتزا",
                KitchenStationId = stationId, Quantity = 1m, UnitPrice = 30.000m, LineTotal = 30.000m
            });
            await context.SaveChangesAsync();

            var service = new KitchenOrderService(unitOfWork);

            var orders = await service.GetPendingOrdersAsync();

            orders.Should().HaveCount(1);
            orders[0].IsPriority.Should().BeTrue();
            orders[0].TableOrType.Should().Be("سفري"); // Takeaway
        }

        [Fact]
        public async Task GetStationsAsync_ReturnsOnlyActive()
        {
            // Arrange
            var dbName = $"POS_KitchenSta_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);

            context.KitchenStations.Add(new KitchenStation { Name = "مطبخ اللحوم", IsActive = true });
            context.KitchenStations.Add(new KitchenStation { Name = "مطبخ المعجنات", IsActive = true });
            context.KitchenStations.Add(new KitchenStation { Name = "مطبخ قديم", IsActive = false });
            await context.SaveChangesAsync();

            var service = new KitchenOrderService(new TestUnitOfWork(context));

            var stations = await service.GetStationsAsync();

            stations.Should().HaveCount(2);
            stations.Should().Contain("مطبخ اللحوم");
            stations.Should().Contain("مطبخ المعجنات");
            stations.Should().NotContain("مطبخ قديم");
        }
    }

    // ========================================================================
    // PRINTER MANAGEMENT SERVICE INTEGRATION TESTS
    // ========================================================================

    public sealed class PrinterManagementServiceIntegrationTests
    {
        [Fact]
        public async Task AddAndGetPrinters_ReturnsAllPrinters()
        {
            // Arrange
            var dbName = $"POS_PrinterTest_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);
            var printerServiceMock = new Mock<IPrinterService>();

            var unitOfWork = new TestUnitOfWork(context);
            var service = new PrinterManagementService(unitOfWork, printerServiceMock.Object, auditMock.Object);

            // Act — add 2 printers
            var printer1 = await service.AddPrinterAsync("طابعة الفواتير", "Thermal", "USB", null, null, 80, "Receipt");
            var printer2 = await service.AddPrinterAsync("طابعة المطبخ", "Thermal", "Network", "192.168.1.50", "9100", 58, "Kitchen");

            // Act — get all
            var all = await service.GetPrintersAsync();

            // Assert — both printers exist
            all.Should().HaveCount(2);
            all.Should().Contain(p => p.Name == "طابعة الفواتير" && p.AssignedRole == "Receipt");
            all.Should().Contain(p => p.Name == "طابعة المطبخ" && p.AssignedRole == "Kitchen" && p.IpAddress == "192.168.1.50");

            // DTO fields mapped correctly
            var receiptPrinter = all.First(p => p.Name == "طابعة الفواتير");
            receiptPrinter.PrinterType.Should().Be("Thermal");
            receiptPrinter.Connection.Should().Be("USB");
            receiptPrinter.PaperWidth.Should().Be(80);
            receiptPrinter.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task UpdatePrinter_PersistsChanges()
        {
            // Arrange
            var dbName = $"POS_PrinterUpd_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);
            var printerServiceMock = new Mock<IPrinterService>();

            var unitOfWork = new TestUnitOfWork(context);
            var service = new PrinterManagementService(unitOfWork, printerServiceMock.Object, auditMock.Object);

            var created = await service.AddPrinterAsync("طابعة أولى", "Thermal", "USB", null, null, 80, "Receipt");

            var updateDto = new PrinterDto(created.Id, "طابعة محدثة", "DotMatrix", "Network", "10.0.0.1", "9100", 58, "Kitchen", false);

            // Act
            var result = await service.UpdatePrinterAsync(updateDto);

            // Assert
            result.Success.Should().BeTrue();

            // Verify via fresh query
            var all = await service.GetPrintersAsync();
            all.Should().HaveCount(1);
            all[0].Name.Should().Be("طابعة محدثة");
            all[0].PrinterType.Should().Be("DotMatrix");
            all[0].Connection.Should().Be("Network");
            all[0].IpAddress.Should().Be("10.0.0.1");
            all[0].PaperWidth.Should().Be(58);
            all[0].AssignedRole.Should().Be("Kitchen"); // Now correctly updated
            all[0].IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeletePrinter_SoftDelete_ExcludedFromQueries()
        {
            // Arrange
            var dbName = $"POS_PrinterDel_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase(dbName).Options;
            await using var context = new POSDbContext(options);
            var auditMock = new Mock<IAuditService>();
            auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);
            var printerServiceMock = new Mock<IPrinterService>();

            var unitOfWork = new TestUnitOfWork(context);
            var service = new PrinterManagementService(unitOfWork, printerServiceMock.Object, auditMock.Object);

            var printer = await service.AddPrinterAsync("للحذف", "Thermal", "USB", null, null, 80, "Receipt");

            // Act — soft delete
            var result = await service.DeletePrinterAsync(printer.Id);
            result.Success.Should().BeTrue();

            // Assert — excluded from normal query
            var all = await service.GetPrintersAsync();
            all.Should().BeEmpty();

            // But still exists in DB (soft delete)
            var printerInDb = await context.Printers.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == printer.Id);
            printerInDb.Should().NotBeNull();
            printerInDb!.IsDeleted.Should().BeTrue();
        }
    }
}
