using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for RecipeService — recipe CRUD, ingredient management, and cost calculation.
///
/// Test areas:
///   1. GetRecipeByProductAsync — product found/not found, recipe exists/not exists
///   2. SaveRecipeAsync — create new, update existing, validation errors
///   3. DeleteRecipeAsync — success / recipe not found
///   4. CalculateRecipeCostAsync — sum of costs / missing items / recipe not found
/// </summary>
public class RecipeServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultProductId = Guid.NewGuid();
    private static readonly Guid DefaultRecipeId = Guid.NewGuid();
    private static readonly Guid DefaultInventoryItemId = Guid.NewGuid();
    private static readonly Guid DefaultInventoryItemId2 = Guid.NewGuid();

    private static Product CreateProduct(Guid? id = null, string name = "منتج اختبار")
    {
        return new Product
        {
            Id = id ?? DefaultProductId,
            ArabicName = name,
            Unit = "piece",
            Status = ProductStatus.Active
        };
    }

    private static Recipe CreateRecipe(
        Guid? id = null,
        Guid? productId = null,
        string name = "وصفة اختبار",
        string? instructions = "تعليمات التحضير")
    {
        return new Recipe
        {
            Id = id ?? DefaultRecipeId,
            ProductId = productId ?? DefaultProductId,
            Name = name,
            Instructions = instructions
        };
    }

    private static RecipeIngredient CreateRecipeIngredient(
        Guid? id = null,
        Guid? recipeId = null,
        Guid? inventoryItemId = null,
        string itemName = "مكون أ",
        decimal quantity = 2.5m,
        string unit = "kg")
    {
        return new RecipeIngredient
        {
            Id = id ?? Guid.NewGuid(),
            RecipeId = recipeId ?? DefaultRecipeId,
            InventoryItemId = inventoryItemId ?? DefaultInventoryItemId,
            Quantity = quantity,
            Unit = unit
        };
    }

    private static InventoryItem CreateInventoryItem(
        Guid? id = null,
        string name = "مادة خام أ",
        decimal cost = 10.000m,
        string unit = "kg")
    {
        return new InventoryItem
        {
            Id = id ?? DefaultInventoryItemId,
            Name = name,
            Unit = unit,
            Cost = cost,
            Quantity = 100m
        };
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
    /// Builds a RecipeService with fully mocked IUnitOfWork and IAuditService.
    /// </summary>
    private (RecipeService service,
             Mock<IUnitOfWork> unitOfWorkMock,
             Mock<IAuditService> auditMock)
        BuildServiceWithMocks(
            Product? product = null,
            Recipe? recipe = null,
            List<RecipeIngredient>? recipeIngredients = null,
            List<InventoryItem>? inventoryItems = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        // ---- Audit (fire-and-forget) ----
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // ---- SaveChanges ----
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Products ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(product);
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- Recipes ----
        var recipeRepoMock = new Mock<IRepository<Recipe>>();
        recipeRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Recipe, bool>>>()))
            .ReturnsAsync(recipe is not null ? new List<Recipe> { recipe } : new List<Recipe>());
        recipeRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(recipe);
        recipeRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Recipe>()))
            .Returns(Task.CompletedTask);
        recipeRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<Recipe>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(recipeRepoMock.Object);

        // ---- RecipeIngredients ----
        var ingredientRepoMock = new Mock<IRepository<RecipeIngredient>>();
        ingredientRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<RecipeIngredient, bool>>>()))
            .ReturnsAsync(recipeIngredients ?? new List<RecipeIngredient>());
        ingredientRepoMock
            .Setup(r => r.AddAsync(It.IsAny<RecipeIngredient>()))
            .Returns(Task.CompletedTask);
        ingredientRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<RecipeIngredient>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(ingredientRepoMock.Object);

        // ---- InventoryItems ----
        var invRepoMock = new Mock<IRepository<InventoryItem>>();
        invRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
                inventoryItems?.FirstOrDefault(i => i.Id == id));
        invRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(inventoryItems ?? new List<InventoryItem>());
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(invRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
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
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryBatches).Returns(CreateEmptyRepoMock<InventoryBatch>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);
        unitOfWorkMock.Setup(u => u.Sales).Returns(CreateEmptyRepoMock<Sale>().Object);
        unitOfWorkMock.Setup(u => u.SalePromotions).Returns(CreateEmptyRepoMock<SalePromotion>().Object);
        unitOfWorkMock.Setup(u => u.Promotions).Returns(CreateEmptyRepoMock<Promotion>().Object);

        var service = new RecipeService(unitOfWorkMock.Object, auditMock.Object);

        return (service, unitOfWorkMock, auditMock);
    }

    // ========================================================================
    // GetRecipeByProductAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetRecipeByProductAsync_ExistingProductWithRecipe_ReturnsDto()
    {
        // Arrange
        var product = CreateProduct();
        var recipe = CreateRecipe(productId: DefaultProductId);
        var ingredient = CreateRecipeIngredient(
            recipeId: recipe.Id,
            inventoryItemId: DefaultInventoryItemId,
            quantity: 2.5m,
            unit: "kg");
        var inventoryItem = CreateInventoryItem(id: DefaultInventoryItemId, name: "دقيق", cost: 3.000m);

        var (service, _, _) = BuildServiceWithMocks(
            product: product,
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient> { ingredient },
            inventoryItems: new List<InventoryItem> { inventoryItem });

        // Act
        var result = await service.GetRecipeByProductAsync(DefaultProductId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(recipe.Id);
        result.ProductId.Should().Be(DefaultProductId);
        result.Name.Should().Be("وصفة اختبار");
        result.Instructions.Should().Be("تعليمات التحضير");
        result.Ingredients.Should().HaveCount(1);
        result.Ingredients[0].ItemName.Should().Be("دقيق");
        result.Ingredients[0].Quantity.Should().Be(2.5m);
        result.Ingredients[0].Unit.Should().Be("kg");

        // Total cost: 2.5 * 3.000 = 7.500
        result.TotalCost.Should().Be(7.500m);
    }

    [Fact]
    public async Task GetRecipeByProductAsync_ProductNotFound_ReturnsNull()
    {
        var (service, _, _) = BuildServiceWithMocks(product: null);
        var result = await service.GetRecipeByProductAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecipeByProductAsync_NoRecipeForProduct_ReturnsNull()
    {
        var product = CreateProduct();
        var (service, _, _) = BuildServiceWithMocks(product: product, recipe: null);
        var result = await service.GetRecipeByProductAsync(DefaultProductId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecipeByProductAsync_MultipleIngredients_CalculatesTotalCost()
    {
        // Arrange
        var product = CreateProduct();
        var recipe = CreateRecipe(productId: DefaultProductId, name: "وصفة متعددة المكونات");
        var invItem1 = CreateInventoryItem(id: DefaultInventoryItemId, name: "دقيق", cost: 3.000m);
        var invItem2 = CreateInventoryItem(id: DefaultInventoryItemId2, name: "سكر", cost: 5.000m);

        var ingredients = new List<RecipeIngredient>
        {
            CreateRecipeIngredient(recipeId: recipe.Id, inventoryItemId: DefaultInventoryItemId, quantity: 2m, unit: "kg"),
            CreateRecipeIngredient(recipeId: recipe.Id, inventoryItemId: DefaultInventoryItemId2, quantity: 1.5m, unit: "kg")
        };

        var (service, _, _) = BuildServiceWithMocks(
            product: product,
            recipe: recipe,
            recipeIngredients: ingredients,
            inventoryItems: new List<InventoryItem> { invItem1, invItem2 });

        // Act
        var result = await service.GetRecipeByProductAsync(DefaultProductId);

        // Assert
        result.Should().NotBeNull();
        result!.Ingredients.Should().HaveCount(2);

        // Total: (2 * 3.000) + (1.5 * 5.000) = 6.000 + 7.500 = 13.500
        result.TotalCost.Should().Be(13.500m);
    }

    [Fact]
    public async Task GetRecipeByProductAsync_IngredientWithUnknownInventory_ReportsZeroCost()
    {
        // Arrange — ingredient references an inventory item not in the inventoryItems list
        var product = CreateProduct();
        var recipe = CreateRecipe(productId: DefaultProductId);
        var ingredient = CreateRecipeIngredient(
            recipeId: recipe.Id,
            inventoryItemId: DefaultInventoryItemId,
            quantity: 5m,
            unit: "piece");

        // No inventory items provided — ingredient's InventoryItemId won't be found
        var (service, _, _) = BuildServiceWithMocks(
            product: product,
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient> { ingredient },
            inventoryItems: new List<InventoryItem>());

        // Act
        var result = await service.GetRecipeByProductAsync(DefaultProductId);

        // Assert — unknown inventory item reports "Unknown" name and zero cost
        result.Should().NotBeNull();
        result!.Ingredients[0].ItemName.Should().Be("Unknown");
        result.TotalCost.Should().Be(0m);
    }

    // ========================================================================
    // SaveRecipeAsync — Create New Tests
    // ========================================================================

    [Fact]
    public async Task SaveRecipeAsync_CreateNew_Success()
    {
        // Arrange
        var product = CreateProduct();
        var invItem = CreateInventoryItem(id: DefaultInventoryItemId, name: "دقيق", cost: 3.000m);

        var ingredients = new List<RecipeIngredientDto>
        {
            new(DefaultInventoryItemId, "دقيق", 2.5m, "kg")
        };

        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks(
            product: product,
            recipe: null,  // no existing recipe → create new
            recipeIngredients: new List<RecipeIngredient>
            {
                // MapToDtoAsync loads ingredients after SaveChangesAsync;
                // provide the expected ingredient so the DTO is populated
                CreateRecipeIngredient(recipeId: DefaultRecipeId, inventoryItemId: DefaultInventoryItemId, quantity: 2.5m, unit: "kg")
            },
            inventoryItems: new List<InventoryItem> { invItem });

        // Act
        var result = await service.SaveRecipeAsync(
            DefaultProductId, "وصفة جديدة", "تعليمات", ingredients);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("وصفة جديدة");
        result.Instructions.Should().Be("تعليمات");
        result.Ingredients.Should().HaveCount(1);
        result.Ingredients[0].ItemName.Should().Be("دقيق");

        // New recipe was added
        unitOfWorkMock.Verify(u => u.Recipes.AddAsync(
            It.Is<Recipe>(r =>
                r.ProductId == DefaultProductId &&
                r.Name == "وصفة جديدة" &&
                r.Instructions == "تعليمات")), Times.Once);

        // Ingredient was added
        unitOfWorkMock.Verify(u => u.RecipeIngredients.AddAsync(
            It.Is<RecipeIngredient>(ri =>
                ri.InventoryItemId == DefaultInventoryItemId &&
                ri.Quantity == 2.5m &&
                ri.Unit == "kg")), Times.Once);

        // Did NOT delete any existing ingredients (since it's a new recipe)
        unitOfWorkMock.Verify(u => u.RecipeIngredients.DeleteAsync(
            It.IsAny<RecipeIngredient>()), Times.Never);

        // SaveChanges called twice (after recipe add, after ingredients add)
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));

        // Audit was logged
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.RecipeChanged, "Recipe",
            It.IsAny<Guid>(), null,
            It.Is<string>(s => s.Contains("وصفة جديدة")),
            null), Times.Once);
    }

    // ========================================================================
    // SaveRecipeAsync — Update Existing Tests
    // ========================================================================

    [Fact]
    public async Task SaveRecipeAsync_UpdateExisting_RemovesOldIngredientsAndAddsNew()
    {
        // Arrange
        var product = CreateProduct();
        var recipe = CreateRecipe(
            id: DefaultRecipeId,
            productId: DefaultProductId,
            name: "وصفة قديمة",
            instructions: "تعليمات قديمة");
        var oldIngredient = CreateRecipeIngredient(
            recipeId: DefaultRecipeId,
            inventoryItemId: Guid.NewGuid(),
            quantity: 1m,
            unit: "piece");
        var invItem = CreateInventoryItem(id: DefaultInventoryItemId, name: "مكون جديد", cost: 8.000m);

        var newIngredients = new List<RecipeIngredientDto>
        {
            new(DefaultInventoryItemId, "مكون جديد", 3m, "kg")
        };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            product: product,
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient> { oldIngredient },
            inventoryItems: new List<InventoryItem> { invItem });

        // Act
        var result = await service.SaveRecipeAsync(
            DefaultProductId, "وصفة محدثة", "تعليمات محدثة", newIngredients);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("وصفة محدثة");
        result.Instructions.Should().Be("تعليمات محدثة");
        result.Ingredients.Should().HaveCount(1);

        // Old ingredient was deleted
        unitOfWorkMock.Verify(u => u.RecipeIngredients.DeleteAsync(
            It.Is<RecipeIngredient>(ri => ri.Id == oldIngredient.Id)), Times.Once);

        // New ingredient was added
        unitOfWorkMock.Verify(u => u.RecipeIngredients.AddAsync(
            It.Is<RecipeIngredient>(ri =>
                ri.InventoryItemId == DefaultInventoryItemId &&
                ri.Quantity == 3m)), Times.Once);

        // Did NOT add a new recipe (updated existing)
        unitOfWorkMock.Verify(u => u.Recipes.AddAsync(
            It.IsAny<Recipe>()), Times.Never);

        // SaveChanges called twice (after ingredient delete, after ingredient add)
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
    }

    // ========================================================================
    // SaveRecipeAsync — Validation Tests
    // ========================================================================

    [Fact]
    public async Task SaveRecipeAsync_NullIngredients_ThrowsArgumentNullException()
    {
        var product = CreateProduct();
        var (service, _, _) = BuildServiceWithMocks(product: product);

        var act = () => service.SaveRecipeAsync(
            DefaultProductId, "Test", null, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveRecipeAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks(product: null);

        var act = () => service.SaveRecipeAsync(
            Guid.NewGuid(), "Test", null, new List<RecipeIngredientDto>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المنتج غير موجود");
    }

    [Fact]
    public async Task SaveRecipeAsync_InventoryItemNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — inventoryItems list is empty, so the referenced item won't be found
        var product = CreateProduct();
        var ingredients = new List<RecipeIngredientDto>
        {
            new(DefaultInventoryItemId, "غير موجود", 1m, "piece")
        };

        var (service, _, _) = BuildServiceWithMocks(
            product: product,
            recipe: null,
            inventoryItems: new List<InventoryItem>());

        // Act
        var act = () => service.SaveRecipeAsync(
            DefaultProductId, "Test", null, ingredients);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"عنصر المخزون (ID: {DefaultInventoryItemId}) غير موجود");
    }

    // ========================================================================
    // DeleteRecipeAsync Tests
    // ========================================================================

    [Fact]
    public async Task DeleteRecipeAsync_ExistingRecipe_DeletesIngredientsAndRecipe()
    {
        // Arrange
        var recipe = CreateRecipe();
        var ingredient1 = CreateRecipeIngredient(recipeId: DefaultRecipeId);
        var ingredient2 = CreateRecipeIngredient(
            recipeId: DefaultRecipeId,
            inventoryItemId: DefaultInventoryItemId2,
            itemName: "مكون ب");

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient> { ingredient1, ingredient2 });

        // Act
        await service.DeleteRecipeAsync(DefaultRecipeId);

        // Assert — all ingredients deleted
        unitOfWorkMock.Verify(u => u.RecipeIngredients.DeleteAsync(
            It.Is<RecipeIngredient>(ri => ri.Id == ingredient1.Id)), Times.Once);
        unitOfWorkMock.Verify(u => u.RecipeIngredients.DeleteAsync(
            It.Is<RecipeIngredient>(ri => ri.Id == ingredient2.Id)), Times.Once);

        // Recipe itself deleted
        unitOfWorkMock.Verify(u => u.Recipes.DeleteAsync(
            It.Is<Recipe>(r => r.Id == DefaultRecipeId)), Times.Once);

        // SaveChanges called once
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteRecipeAsync_NonExistentRecipe_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks(recipe: null);

        var act = () => service.DeleteRecipeAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوصفة غير موجودة");
    }

    [Fact]
    public async Task DeleteRecipeAsync_RecipeWithNoIngredients_DeletesRecipeOnly()
    {
        // Arrange
        var recipe = CreateRecipe();

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient>()); // no ingredients

        // Act
        await service.DeleteRecipeAsync(DefaultRecipeId);

        // Assert — no ingredients to delete
        unitOfWorkMock.Verify(u => u.RecipeIngredients.DeleteAsync(
            It.IsAny<RecipeIngredient>()), Times.Never);

        // Recipe was deleted
        unitOfWorkMock.Verify(u => u.Recipes.DeleteAsync(
            It.Is<Recipe>(r => r.Id == DefaultRecipeId)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ========================================================================
    // CalculateRecipeCostAsync Tests
    // ========================================================================

    [Fact]
    public async Task CalculateRecipeCostAsync_ExistingRecipe_ReturnsSumOfCosts()
    {
        // Arrange
        var recipe = CreateRecipe(name: "وصفة التكلفة");
        var invItem1 = CreateInventoryItem(id: DefaultInventoryItemId, name: "دقيق", cost: 3.000m);
        var invItem2 = CreateInventoryItem(id: DefaultInventoryItemId2, name: "سكر", cost: 5.000m);

        var ingredients = new List<RecipeIngredient>
        {
            CreateRecipeIngredient(recipeId: DefaultRecipeId, inventoryItemId: DefaultInventoryItemId, quantity: 2m, unit: "kg"),
            CreateRecipeIngredient(recipeId: DefaultRecipeId, inventoryItemId: DefaultInventoryItemId2, quantity: 1.5m, unit: "kg")
        };

        var (service, _, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: ingredients,
            inventoryItems: new List<InventoryItem> { invItem1, invItem2 });

        // Act
        var cost = await service.CalculateRecipeCostAsync(DefaultRecipeId);

        // Assert — (2 * 3.000) + (1.5 * 5.000) = 6.000 + 7.500 = 13.500
        cost.Should().Be(13.500m);
    }

    [Fact]
    public async Task CalculateRecipeCostAsync_NonExistentRecipe_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks(recipe: null);

        var act = () => service.CalculateRecipeCostAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوصفة غير موجودة");
    }

    [Fact]
    public async Task CalculateRecipeCostAsync_IngredientWithoutInventoryItem_SkipsCost()
    {
        // Arrange — ingredient references an inventory item not in the inventoryItems list
        var recipe = CreateRecipe();
        var ingredients = new List<RecipeIngredient>
        {
            CreateRecipeIngredient(
                recipeId: DefaultRecipeId,
                inventoryItemId: Guid.NewGuid(),  // not in inventoryItems list
                quantity: 5m,
                unit: "piece")
        };

        var (service, _, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: ingredients,
            inventoryItems: new List<InventoryItem>());

        // Act
        var cost = await service.CalculateRecipeCostAsync(DefaultRecipeId);

        // Assert — inventory item not found in dictionary, cost contribution = 0
        cost.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateRecipeCostAsync_EmptyIngredients_ReturnsZero()
    {
        // Arrange
        var recipe = CreateRecipe();

        var (service, _, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: new List<RecipeIngredient>(),
            inventoryItems: new List<InventoryItem>());

        // Act
        var cost = await service.CalculateRecipeCostAsync(DefaultRecipeId);

        // Assert
        cost.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateRecipeCostAsync_ZeroCostInventoryItems_ReturnsZero()
    {
        // Arrange
        var recipe = CreateRecipe();
        var invItem = CreateInventoryItem(id: DefaultInventoryItemId, name: "مجاني", cost: 0m);

        var ingredients = new List<RecipeIngredient>
        {
            CreateRecipeIngredient(recipeId: DefaultRecipeId, inventoryItemId: DefaultInventoryItemId, quantity: 10m, unit: "piece")
        };

        var (service, _, _) = BuildServiceWithMocks(
            recipe: recipe,
            recipeIngredients: ingredients,
            inventoryItems: new List<InventoryItem> { invItem });

        // Act
        var cost = await service.CalculateRecipeCostAsync(DefaultRecipeId);

        // Assert — 10 * 0 = 0
        cost.Should().Be(0m);
    }
}
