#nullable enable

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
/// Unit tests for ProductService covering all 10 public methods:
/// GetProductsAsync, GetProductByIdAsync, CreateProductAsync, UpdateProductAsync,
/// ArchiveProductAsync, FindByBarcodeAsync, FindBySkuAsync, GetLowStockProductsAsync,
/// GetCategoriesAsync, CreateCategoryAsync.
/// </summary>
public class ProductServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultCategoryId = Guid.NewGuid();
    private static readonly Guid DefaultSupplierId = Guid.NewGuid();

    /// <summary>
    /// Creates a test product with the given properties.
    /// </summary>
    private static Product CreateTestProduct(
        Guid? id = null,
        string arabicName = "قهوة تركية",
        string name = "Turkish Coffee",
        string sku = "COF-001",
        string? barcode = "1234567890123",
        Guid? categoryId = null,
        ProductStatus status = ProductStatus.Active,
        decimal price = 10.000m,
        decimal cost = 5.000m,
        decimal taxRate = 0.16m,
        decimal minStock = 5m,
        Guid? supplierId = null)
    {
        return new Product
        {
            Id = id ?? Guid.NewGuid(),
            ArabicName = arabicName,
            Name = name,
            Sku = sku,
            Barcode = barcode,
            CategoryId = categoryId ?? DefaultCategoryId,
            ProductType = ProductType.Standard,
            Unit = "piece",
            Cost = cost,
            Price = price,
            TaxRate = taxRate,
            MinStock = minStock,
            SupplierId = supplierId,
            Status = status,
            AllowModifiers = false
        };
    }

    /// <summary>
    /// Creates a test category.
    /// </summary>
    private static Category CreateTestCategory(
        Guid? id = null,
        string name = "مشروبات ساخنة",
        int sortOrder = 0,
        bool isActive = true,
        Guid? parentId = null)
    {
        return new Category
        {
            Id = id ?? DefaultCategoryId,
            Name = name,
            SortOrder = sortOrder,
            IsActive = isActive,
            ParentCategoryId = parentId
        };
    }

    /// <summary>
    /// Creates a test inventory item.
    /// </summary>
    private static InventoryItem CreateTestInventory(Guid productId, decimal quantity = 50m)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            ReservedQuantity = 0
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
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a ProductService with fully mocked IUnitOfWork and IAuditService.
    /// </summary>
    private (ProductService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            List<Product>? products = null,
            List<Category>? categories = null,
            List<InventoryItem>? inventoryItems = null,
            List<Supplier>? suppliers = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Products repository ----
        var productRepoMock = new Mock<IRepository<Product>>();
        productRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(products ?? new List<Product>());
        productRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => products?.FirstOrDefault(p => p.Id == id));
        productRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Product, bool>>>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate) =>
                (products ?? new List<Product>()).AsQueryable().Where(predicate).ToList());
        productRepoMock.Setup(r => r.AddAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
        productRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Products).Returns(productRepoMock.Object);

        // ---- Categories repository ----
        var categoryRepoMock = new Mock<IRepository<Category>>();
        categoryRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(categories ?? new List<Category>());
        categoryRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => categories?.FirstOrDefault(c => c.Id == id));
        categoryRepoMock.Setup(r => r.AddAsync(It.IsAny<Category>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Categories).Returns(categoryRepoMock.Object);

        // ---- InventoryItems repository ----
        var inventoryRepoMock = new Mock<IRepository<InventoryItem>>();
        inventoryRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(inventoryItems ?? new List<InventoryItem>());
        inventoryRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InventoryItem, bool>>>()))
            .ReturnsAsync((Expression<Func<InventoryItem, bool>> predicate) =>
                (inventoryItems ?? new List<InventoryItem>()).AsQueryable().Where(predicate).ToList());
        inventoryRepoMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(inventoryRepoMock.Object);

        // ---- Suppliers repository ----
        var supplierRepoMock = new Mock<IRepository<Supplier>>();
        supplierRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => suppliers?.FirstOrDefault(s => s.Id == id));
        supplierRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(suppliers ?? new List<Supplier>());
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(supplierRepoMock.Object);

        // ---- Stub remaining repos to prevent NullReferenceException ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Sales).Returns(CreateEmptyRepoMock<Sale>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
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

        var service = new ProductService(unitOfWorkMock.Object, auditServiceMock.Object);
        return (service, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // GetProductsAsync — Search, Filter, Pagination
    // ========================================================================

    [Fact]
    public async Task GetProductsAsync_SearchByArabicName_FiltersResults()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var products = new List<Product>
        {
            CreateTestProduct(arabicName: "قهوة تركية", categoryId: catId),
            CreateTestProduct(arabicName: "شاي أحمر", sku: "TEA-001", categoryId: catId),
            CreateTestProduct(arabicName: "قهوة مثلجة", sku: "COLD-001", categoryId: catId)
        };
        var categories = new List<Category> { CreateTestCategory(catId, sortOrder: 1) };
        var inventory = products.Select(p => CreateTestInventory(p.Id, quantity: 50m)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: "قهوة", CategoryId: null, ProductType: null, Status: null);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert — 2 coffee products match "قهوة", 1 tea does not
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(p => p.Name == "قهوة تركية");
        result.Items.Should().Contain(p => p.Name == "قهوة مثلجة");
    }

    [Fact]
    public async Task GetProductsAsync_SearchByEnglishName_FiltersResults()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(name: "Turkish Coffee"),
            CreateTestProduct(name: "Red Tea", sku: "TEA-001"),
            CreateTestProduct(name: "Iced Coffee", sku: "COLD-001")
        };
        var categories = new List<Category> { CreateTestCategory() };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: "Coffee", CategoryId: null, ProductType: null, Status: null);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert — 2 coffee products
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProductsAsync_SearchBySku_FiltersResults()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(sku: "COF-001"),
            CreateTestProduct(sku: "TEA-001"),
            CreateTestProduct(sku: "COLD-001")
        };
        var categories = new List<Category> { CreateTestCategory() };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: "TEA", CategoryId: null, ProductType: null, Status: null);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().Sku.Should().Be("TEA-001");
    }

    [Fact]
    public async Task GetProductsAsync_FilterByCategory_FiltersResults()
    {
        // Arrange
        var coffeeCatId = Guid.NewGuid();
        var teaCatId = Guid.NewGuid();
        var products = new List<Product>
        {
            CreateTestProduct(arabicName: "قهوة تركية", categoryId: coffeeCatId),
            CreateTestProduct(arabicName: "شاي أحمر", sku: "TEA-001", categoryId: teaCatId),
            CreateTestProduct(arabicName: "قهوة أمريكية", categoryId: coffeeCatId)
        };
        var categories = new List<Category>
        {
            CreateTestCategory(coffeeCatId, name: "قهوة", sortOrder: 1),
            CreateTestCategory(teaCatId, name: "شاي", sortOrder: 2)
        };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: null, CategoryId: coffeeCatId, ProductType: null, Status: null);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert — 2 coffee products, 0 tea
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProductsAsync_FilterByStatus_FiltersResults()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(status: ProductStatus.Active),
            CreateTestProduct(status: ProductStatus.Archived, sku: "ARC-001"),
            CreateTestProduct(status: ProductStatus.Active, sku: "COF-002")
        };
        var categories = new List<Category> { CreateTestCategory() };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: null, CategoryId: null, ProductType: null, Status: "Active");

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert — only 2 active products
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.All(p => p.Status == "Active").Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_Pagination_RespectsPageSize()
    {
        // Arrange — 10 products, page 1 with pageSize 3
        var products = Enumerable.Range(1, 10)
            .Select(i => CreateTestProduct(
                arabicName: $"منتج {i}",
                sku: $"PRD-{i:D3}"))
            .ToList();
        var categories = new List<Category> { CreateTestCategory() };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: null, CategoryId: null, ProductType: null, Status: null,
            Page: 1, PageSize: 3);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);

        // Page 2 should have 3 items
        var filter2 = filter with { Page = 2 };
        var result2 = await service.GetProductsAsync(filter2);
        result2.Items.Should().HaveCount(3);
        result2.Page.Should().Be(2);

        // Page 4 should have 1 item (last page)
        var filter4 = filter with { Page = 4 };
        var result4 = await service.GetProductsAsync(filter4);
        result4.Items.Should().HaveCount(1);
        result4.Page.Should().Be(4);
    }

    [Fact]
    public async Task GetProductsAsync_NoMatchingProducts_ReturnsEmpty()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(arabicName: "قهوة تركية")
        };
        var categories = new List<Category> { CreateTestCategory() };
        var inventory = products.Select(p => CreateTestInventory(p.Id)).ToList();

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        var filter = new ProductFilterDto(SearchTerm: "شاي", CategoryId: null, ProductType: null, Status: null);

        // Act
        var result = await service.GetProductsAsync(filter);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ========================================================================
    // GetProductByIdAsync — Single Product Lookup
    // ========================================================================

    [Fact]
    public async Task GetProductByIdAsync_ProductExists_ReturnsDto()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = CreateTestProduct(productId, arabicName: "قهوة تركية", barcode: "12345");
        var category = CreateTestCategory(name: "مشروبات");
        var inventory = CreateTestInventory(productId, quantity: 30m);

        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            categories: new List<Category> { category },
            inventoryItems: new List<InventoryItem> { inventory });

        // Act
        var result = await service.GetProductByIdAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        // MapToDto swaps: product.ArabicName → Dto.Name, product.Name → Dto.ArabicName
        result.Name.Should().Be("قهوة تركية");
        result.ArabicName.Should().Be("Turkish Coffee");
        result.Barcode.Should().Be("12345");
        result.CurrentStock.Should().Be(30m);
        result.CategoryName.Should().Be("مشروبات");
    }

    [Fact]
    public async Task GetProductByIdAsync_ProductNotFound_ReturnsNull()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetProductByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // CreateProductAsync — Create + Inventory + Audit Trail
    // ========================================================================

    [Fact]
    public async Task CreateProductAsync_HappyPath_CreatesProductAndInventoryAndAudit()
    {
        // Arrange
        var category = CreateTestCategory();
        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            categories: new List<Category> { category });

        var request = new CreateProductRequest(
            Name: "Turkish Coffee",
            ArabicName: "قهوة تركية",
            Sku: "COF-001",
            Barcode: "1234567890123",
            CategoryId: DefaultCategoryId,
            ProductType: "Standard",
            Unit: "cup",
            Cost: 5.000m,
            Price: 10.000m,
            TaxRate: 16m,  // 16% → stored as 0.16
            MinStock: 5m,
            SupplierId: null,
            AllowModifiers: true);

        // Act
        var result = await service.CreateProductAsync(request);

        // Assert — returned DTO has correct values
        // MapToDto swaps: request.ArabicName → Dto.Name, request.Name → Dto.ArabicName
        result.Should().NotBeNull();
        result.Name.Should().Be("قهوة تركية");
        result.ArabicName.Should().Be("Turkish Coffee");
        result.Sku.Should().Be("COF-001");
        result.Barcode.Should().Be("1234567890123");
        result.Price.Should().Be(10.000m);
        result.Cost.Should().Be(5.000m);
        result.TaxRate.Should().Be(0.16m); // Divided by 100
        result.AllowModifiers.Should().BeTrue();
        result.Status.Should().Be("Active");

        // Product was added
        unitOfWorkMock.Verify(u => u.Products.AddAsync(
            It.Is<Product>(p =>
                p.Name == "Turkish Coffee" &&
                p.ArabicName == "قهوة تركية" &&
                p.Price == 10.000m &&
                p.TaxRate == 0.16m &&
                p.Status == ProductStatus.Active)), Times.Once);

        // Inventory record was created with quantity 0
        unitOfWorkMock.Verify(u => u.InventoryItems.AddAsync(
            It.Is<InventoryItem>(inv =>
                inv.Quantity == 0m &&
                inv.ReservedQuantity == 0m)), Times.Once);

        // Audit was logged with ProductCreated
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.ProductCreated,
            "Product",
            It.IsAny<Guid>(),
            null,
            null,
            null), Times.Once);

        // SaveChanges called twice (product + inventory)
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateProductAsync_InvalidRequest_ThrowsInvalidOperationException()
    {
        // Arrange — missing Arabic name and negative price
        var (service, _, _) = BuildServiceWithMocks();

        var request = new CreateProductRequest(
            Name: null!, ArabicName: "", Sku: null!, Barcode: null,
            CategoryId: null, ProductType: "Standard", Unit: null,
            Cost: 5m, Price: -10m, TaxRate: 16m, MinStock: 0,
            SupplierId: null, AllowModifiers: false);

        // Act
        var act = () => service.CreateProductAsync(request);

        // Assert — ProductValidator errors thrown
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("اسم المنتج بالعربية مطلوب, سعر البيع يجب أن يكون 0 أو أكبر");
    }

    [Fact]
    public async Task CreateProductAsync_WithSupplier_SetsSupplierId()
    {
        // Arrange
        var category = CreateTestCategory();
        var supplier = new Supplier
        {
            Id = DefaultSupplierId,
            Name = "المورد الرئيسي",
            IsActive = true
        };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            categories: new List<Category> { category },
            suppliers: new List<Supplier> { supplier });

        var request = new CreateProductRequest(
            Name: "Test", ArabicName: "اختبار", Sku: "TST-001", Barcode: null,
            CategoryId: DefaultCategoryId, ProductType: "Standard", Unit: "piece",
            Cost: 5m, Price: 10m, TaxRate: 0, MinStock: 0,
            SupplierId: DefaultSupplierId, AllowModifiers: false);

        // Act
        await service.CreateProductAsync(request);

        // Assert — SupplierId was set
        unitOfWorkMock.Verify(u => u.Products.AddAsync(
            It.Is<Product>(p => p.SupplierId == DefaultSupplierId)), Times.Once);
    }

    [Fact]
    public async Task CreateProductAsync_InvalidProductType_FallsBackToStandard()
    {
        // Arrange
        var category = CreateTestCategory();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            categories: new List<Category> { category });

        var request = new CreateProductRequest(
            Name: "Test", ArabicName: "اختبار", Sku: "TST-001", Barcode: null,
            CategoryId: DefaultCategoryId, ProductType: "InvalidType", Unit: "piece",
            Cost: 5m, Price: 10m, TaxRate: 0, MinStock: 0,
            SupplierId: null, AllowModifiers: false);

        // Act
        await service.CreateProductAsync(request);

        // Assert — falls back to Standard
        unitOfWorkMock.Verify(u => u.Products.AddAsync(
            It.Is<Product>(p => p.ProductType == ProductType.Standard)), Times.Once);
    }

    // ========================================================================
    // UpdateProductAsync — Update + Audit Trail
    // ========================================================================

    [Fact]
    public async Task UpdateProductAsync_HappyPath_UpdatesAndLogsAudit()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = CreateTestProduct(productId, arabicName: "قهوة قديمة", price: 8.000m, sku: "OLD-001");
        var category = CreateTestCategory(name: "مشروبات");
        var inventory = CreateTestInventory(productId, quantity: 20m);

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            products: new List<Product> { product },
            categories: new List<Category> { category },
            inventoryItems: new List<InventoryItem> { inventory });

        var request = new UpdateProductRequest(
            Id: productId, Name: "New Coffee", ArabicName: "قهوة جديدة",
            Sku: "NEW-001", Barcode: "987654321",
            CategoryId: DefaultCategoryId, ProductType: "Standard", Unit: "cup",
            Cost: 6.000m, Price: 12.000m, TaxRate: 16m, MinStock: 3m,
            SupplierId: null, AllowModifiers: false, Status: "Active");

        // Act
        var result = await service.UpdateProductAsync(request);

        // Assert — returned DTO has updated values
        // MapToDto swaps: product.ArabicName → Dto.Name, product.Name → Dto.ArabicName
        result.Should().NotBeNull();
        result.Name.Should().Be("قهوة جديدة");
        result.ArabicName.Should().Be("New Coffee");
        result.Sku.Should().Be("NEW-001");
        result.Price.Should().Be(12.000m);

        // Original product object was mutated
        product.ArabicName.Should().Be("قهوة جديدة");
        product.Price.Should().Be(12.000m);
        product.Sku.Should().Be("NEW-001");
        product.TaxRate.Should().Be(0.16m);

        // UpdateAsync was called
        unitOfWorkMock.Verify(u => u.Products.UpdateAsync(product), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        // Audit was logged with before/after values
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.ProductUpdated,
            "Product",
            productId,
            "ArabicName=قهوة قديمة,SellingPrice=8.000",
            "ArabicName=قهوة جديدة,SellingPrice=12.000",
            null), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ProductNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — no products in repo
        var (service, _, _) = BuildServiceWithMocks();

        var request = new UpdateProductRequest(
            Id: Guid.NewGuid(), Name: "Test", ArabicName: "اختبار",
            Sku: "TST", Barcode: null, CategoryId: null, ProductType: "Standard",
            Unit: "piece", Cost: 5m, Price: 10m, TaxRate: 0, MinStock: 0,
            SupplierId: null, AllowModifiers: false, Status: "Active");

        // Act
        var act = () => service.UpdateProductAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المنتج غير موجود");
    }

    [Fact]
    public async Task UpdateProductAsync_InvalidStatus_FallsBackToExisting()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = CreateTestProduct(productId, status: ProductStatus.Active);
        var inventory = CreateTestInventory(productId);

        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            categories: new List<Category> { CreateTestCategory() },
            inventoryItems: new List<InventoryItem> { inventory });

        var request = new UpdateProductRequest(
            Id: productId, Name: "Test", ArabicName: "اختبار",
            Sku: "TST", Barcode: null, CategoryId: DefaultCategoryId, ProductType: "Standard",
            Unit: "piece", Cost: 5m, Price: 10m, TaxRate: 0, MinStock: 0,
            SupplierId: null, AllowModifiers: false, Status: "InvalidStatus");

        // Act
        var result = await service.UpdateProductAsync(request);

        // Assert — status remained Active (unchanged)
        product.Status.Should().Be(ProductStatus.Active);
    }

    // ========================================================================
    // ArchiveProductAsync — Soft Delete
    // ========================================================================

    [Fact]
    public async Task ArchiveProductAsync_ProductExists_ArchivesAndLogsAudit()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = CreateTestProduct(productId, status: ProductStatus.Active);

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            products: new List<Product> { product });

        // Act
        var result = await service.ArchiveProductAsync(productId, "Discontinued");

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SuccessMessage.Should().Be("تم أرشفة المنتج بنجاح");

        // Product status was changed to Archived
        product.Status.Should().Be(ProductStatus.Archived);

        unitOfWorkMock.Verify(u => u.Products.UpdateAsync(
            It.Is<Product>(p => p.Status == ProductStatus.Archived)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.ProductArchived,
            "Product",
            productId,
            null,
            null,
            "Discontinued"), Times.Once);
    }

    [Fact]
    public async Task ArchiveProductAsync_ProductNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.ArchiveProductAsync(Guid.NewGuid(), "reason");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("المنتج غير موجود");
    }

    // ========================================================================
    // FindByBarcodeAsync — Barcode Lookup
    // ========================================================================

    [Fact]
    public async Task FindByBarcodeAsync_BarcodeFound_ReturnsProductDto()
    {
        // Arrange
        var product = CreateTestProduct(barcode: "1234567890123");
        var inventory = CreateTestInventory(product.Id, quantity: 25m);
        var category = CreateTestCategory();

        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            categories: new List<Category> { category },
            inventoryItems: new List<InventoryItem> { inventory });

        // Act
        var result = await service.FindByBarcodeAsync("1234567890123");

        // Assert
        result.Should().NotBeNull();
        result!.Barcode.Should().Be("1234567890123");
        result.CurrentStock.Should().Be(25m);
    }

    [Fact]
    public async Task FindByBarcodeAsync_EmptyString_ReturnsNull()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.FindByBarcodeAsync("");

        // Assert — guard clause returns null early
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByBarcodeAsync_BarcodeNotFound_ReturnsNull()
    {
        // Arrange
        var product = CreateTestProduct(barcode: "1234567890123");
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product });

        // Act — search for different barcode
        var result = await service.FindByBarcodeAsync("9999999999999");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByBarcodeAsync_InactiveProduct_ReturnsNull()
    {
        // Arrange — inactive product with matching barcode
        var product = CreateTestProduct(barcode: "1234567890123", status: ProductStatus.Archived);
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product });

        // Act
        var result = await service.FindByBarcodeAsync("1234567890123");

        // Assert — FindAsync filters by Active status
        result.Should().BeNull();
    }

    // ========================================================================
    // FindBySkuAsync — SKU Lookup
    // ========================================================================

    [Fact]
    public async Task FindBySkuAsync_SkuFound_ReturnsProductDto()
    {
        // Arrange
        var product = CreateTestProduct(sku: "COF-001");
        var inventory = CreateTestInventory(product.Id);
        var category = CreateTestCategory();

        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product },
            categories: new List<Category> { category },
            inventoryItems: new List<InventoryItem> { inventory });

        // Act
        var result = await service.FindBySkuAsync("COF-001");

        // Assert
        result.Should().NotBeNull();
        result!.Sku.Should().Be("COF-001");
    }

    [Fact]
    public async Task FindBySkuAsync_EmptyString_ReturnsNull()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.FindBySkuAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindBySkuAsync_SkuNotFound_ReturnsNull()
    {
        // Arrange
        var product = CreateTestProduct(sku: "COF-001");
        var (service, _, _) = BuildServiceWithMocks(
            products: new List<Product> { product });

        // Act
        var result = await service.FindBySkuAsync("NONEXISTENT");

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // GetLowStockProductsAsync — Low Stock Calculation
    // ========================================================================

    [Fact]
    public async Task GetLowStockProductsAsync_SomeProductsLow_ReturnsFilteredList()
    {
        // Arrange
        var products = new List<Product>
        {
            CreateTestProduct(arabicName: "قهوة", sku: "COF-001", minStock: 10m),  // stock=50 → ok
            CreateTestProduct(arabicName: "شاي", sku: "TEA-001", minStock: 10m),   // stock=5 → low!
            CreateTestProduct(arabicName: "سكر", sku: "SUG-001", minStock: 20m)    // stock=50 → ok
        };
        var inventory = new List<InventoryItem>
        {
            CreateTestInventory(products[0].Id, quantity: 50m),
            CreateTestInventory(products[1].Id, quantity: 5m),  // below minStock 10
            CreateTestInventory(products[2].Id, quantity: 50m)
        };
        var categories = new List<Category> { CreateTestCategory() };

        var (service, _, _) = BuildServiceWithMocks(products, categories, inventory);

        // Act
        var result = await service.GetLowStockProductsAsync();

        // Assert — only tea (qty=5 <= minStock=10) is low
        result.Should().HaveCount(1);
        result.First().Sku.Should().Be("TEA-001");
    }

    [Fact]
    public async Task GetLowStockProductsAsync_AllSufficient_ReturnsEmpty()
    {
        // Arrange — all products have stock above minStock
        var products = new List<Product>
        {
            CreateTestProduct(minStock: 5m),
            CreateTestProduct(minStock: 10m, sku: "TEA-001")
        };
        var inventory = new List<InventoryItem>
        {
            CreateTestInventory(products[0].Id, quantity: 50m),
            CreateTestInventory(products[1].Id, quantity: 20m)
        };

        var (service, _, _) = BuildServiceWithMocks(products,
            categories: new List<Category> { CreateTestCategory() },
            inventoryItems: inventory);

        // Act
        var result = await service.GetLowStockProductsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLowStockProductsAsync_InactiveProducts_Excluded()
    {
        // Arrange — inactive product with stock below minStock
        var products = new List<Product>
        {
            CreateTestProduct(status: ProductStatus.Archived, minStock: 10m, sku: "ARC-001")
        };
        var inventory = new List<InventoryItem>
        {
            CreateTestInventory(products[0].Id, quantity: 2m)
        };

        var (service, _, _) = BuildServiceWithMocks(products,
            categories: new List<Category> { CreateTestCategory() },
            inventoryItems: inventory);

        // Act
        var result = await service.GetLowStockProductsAsync();

        // Assert — archived product excluded even though stock is low
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLowStockProductsAsync_NoInventoryRecord_ProductNotLow()
    {
        // Arrange — product exists but no inventory record
        var products = new List<Product>
        {
            CreateTestProduct(minStock: 5m)
        };
        // No inventory items passed

        var (service, _, _) = BuildServiceWithMocks(products,
            categories: new List<Category> { CreateTestCategory() });

        // Act
        var result = await service.GetLowStockProductsAsync();

        // Assert — without inventory record, TryGetValue fails → product excluded
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetCategoriesAsync — Category List with Product Counts
    // ========================================================================

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategoriesWithProductCounts()
    {
        // Arrange
        var cat1Id = Guid.NewGuid();
        var cat2Id = Guid.NewGuid();

        var categories = new List<Category>
        {
            CreateTestCategory(cat1Id, name: "قهوة", sortOrder: 2),
            CreateTestCategory(cat2Id, name: "شاي", sortOrder: 1)
        };
        var products = new List<Product>
        {
            CreateTestProduct(categoryId: cat1Id, arabicName: "قهوة 1"),
            CreateTestProduct(categoryId: cat1Id, arabicName: "قهوة 2"),
            CreateTestProduct(categoryId: cat2Id, arabicName: "شاي 1")
        };

        var (service, _, _) = BuildServiceWithMocks(products, categories);

        // Act
        var result = await service.GetCategoriesAsync();

        // Assert — ordered by SortOrder (شاي=1 first, قهوة=2 second)
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("شاي");        // SortOrder 1
        result[0].ProductCount.Should().Be(1);
        result[1].Name.Should().Be("قهوة");       // SortOrder 2
        result[1].ProductCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCategoriesAsync_InactiveCategories_Excluded()
    {
        // Arrange
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();

        var categories = new List<Category>
        {
            CreateTestCategory(activeId, name: "نشط", sortOrder: 1, isActive: true),
            CreateTestCategory(inactiveId, name: "غير نشط", sortOrder: 2, isActive: false)
        };

        var (service, _, _) = BuildServiceWithMocks(
            categories: categories);

        // Act
        var result = await service.GetCategoriesAsync();

        // Assert — only active category returned
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("نشط");
    }

    [Fact]
    public async Task GetCategoriesAsync_Empty_ReturnsEmptyList()
    {
        // Arrange — no categories
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetCategoriesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // CreateCategoryAsync — Create Category
    // ========================================================================

    [Fact]
    public async Task CreateCategoryAsync_WithParentId_CreatesCategory()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks();

        // Act
        var result = await service.CreateCategoryAsync("مشروبات ساخنة", parentId);

        // Assert — returned DTO
        result.Name.Should().Be("مشروبات ساخنة");
        result.ParentId.Should().Be(parentId);
        result.IsActive.Should().BeTrue();
        result.SortOrder.Should().Be(0);
        result.ProductCount.Should().Be(0);

        // Category was added with correct properties
        unitOfWorkMock.Verify(u => u.Categories.AddAsync(
            It.Is<Category>(c =>
                c.Name == "مشروبات ساخنة" &&
                c.ParentCategoryId == parentId &&
                c.IsActive == true &&
                c.SortOrder == 0)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateCategoryAsync_WithoutParentId_CreatesRootCategory()
    {
        // Arrange
        var (service, unitOfWorkMock, _) = BuildServiceWithMocks();

        // Act
        var result = await service.CreateCategoryAsync("مشروبات", null);

        // Assert
        result.Name.Should().Be("مشروبات");
        result.ParentId.Should().BeNull();

        unitOfWorkMock.Verify(u => u.Categories.AddAsync(
            It.Is<Category>(c => c.ParentCategoryId == null)), Times.Once);
    }
}
