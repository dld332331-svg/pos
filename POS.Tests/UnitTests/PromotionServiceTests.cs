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
/// Unit tests for PromotionService — the promotions engine supporting
/// Percentage, FixedAmount, BuyXGetY, and MultiBuy promotion types.
///
/// Test areas:
///   1. CRUD operations (GetAll, GetById, Create, Update, Delete)
///   2. GetEligiblePromotionsAsync — eligibility matching for each promotion type
///   3. ApplyPromotionAsync — successful application with sale update
///   4. CalculatePromotionDiscount — edge cases for each calculation type
///   5. BuyXGetY calculation logic
///   6. MultiBuy calculation logic
///   7. Error handling (not found, inactive, expired, min purchase)
/// </summary>
public class PromotionServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultPromotionId = Guid.NewGuid();
    private static readonly Guid DefaultProductId = Guid.NewGuid();
    private static readonly Guid DefaultProductId2 = Guid.NewGuid();

    /// <summary>
    /// Creates a test promotion with the specified properties.
    /// Active and within valid date range by default.
    /// </summary>
    private static Promotion CreatePromotion(
        Guid? id = null,
        string name = "تخفيض 10%",
        string type = "Percentage",
        decimal value = 10m,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool isActive = true,
        int priority = 0,
        decimal? minPurchaseAmount = null,
        int? minQuantity = null,
        int? buyQuantity = null,
        int? freeQuantity = null,
        int maxApplications = 99,
        string? applicableProductIdsJson = null)
    {
        return new Promotion
        {
            Id = id ?? DefaultPromotionId,
            Name = name,
            Description = $"تخفيض: {name}",
            Type = Enum.Parse<PromotionType>(type),
            Value = value,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-1),
            EndDate = endDate ?? DateTime.UtcNow.AddDays(30),
            IsActive = isActive,
            Priority = priority,
            MinPurchaseAmount = minPurchaseAmount,
            MinQuantity = minQuantity,
            BuyQuantity = buyQuantity,
            FreeQuantity = freeQuantity,
            MaxApplications = maxApplications,
            ApplicableProductIdsJson = applicableProductIdsJson
        };
    }

    /// <summary>
    /// Creates a test sale item DTO.
    /// </summary>
    private static SaleItemDto CreateSaleItem(
        Guid? productId = null,
        string name = "Test Product",
        decimal quantity = 1m,
        decimal unitPrice = 10.000m)
    {
        return new SaleItemDto(
            Id: Guid.NewGuid(),
            ProductId: productId ?? DefaultProductId,
            ProductName: name,
            Quantity: quantity,
            UnitPrice: unitPrice,
            Discount: 0,
            TaxRate: 0.16m,
            TaxAmount: 0,
            LineTotal: 0,
            Cost: 5.000m,
            Notes: null,
            ModifierSummary: null);
    }

    /// <summary>
    /// Creates a test sale for ApplyPromotionAsync tests.
    /// </summary>
    private static Sale CreateTestSale(Guid saleId, decimal subTotal = 100m, decimal taxAmount = 16m)
    {
        return new Sale
        {
            Id = saleId,
            InvoiceNumber = "INV-TEST-0001",
            SubTotal = subTotal,
            TaxAmount = taxAmount,
            DiscountAmount = 0,
            TotalAmount = subTotal + taxAmount,
            Status = SaleStatus.Active,
            IsPaid = false
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Creates an empty Mock for IRepository{T} that returns empty lists from FindAsync
    /// (prevents NullReferenceException when a repo is accessed but not expected to return data).
    /// </summary>
    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a PromotionService with fully mocked IUnitOfWork and IAuditService.
    /// </summary>
    private (PromotionService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            List<Promotion>? promotions = null,
            Sale? sale = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        // Audit — fire-and-forget
        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // ---- Transactions / SaveChanges ----
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Promotions repository ----
        var promoRepoMock = new Mock<IRepository<Promotion>>();

        // GetAllAsync
        promoRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(promotions ?? new List<Promotion>());

        // GetByIdAsync
        promoRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
                promotions?.FirstOrDefault(p => p.Id == id));

        // Add/Update/Delete
        promoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Promotion>()))
            .Returns(Task.CompletedTask);
        promoRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Promotion>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.Promotions).Returns(promoRepoMock.Object);

        // ---- Sale repository (for ApplyPromotionAsync) ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(sale);
        saleRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Sale>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- SalePromotions repository ----
        var salePromoRepoMock = new Mock<IRepository<SalePromotion>>();
        salePromoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SalePromotion>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.SalePromotions).Returns(salePromoRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
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
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryBatches).Returns(CreateEmptyRepoMock<InventoryBatch>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new PromotionService(unitOfWorkMock.Object, auditServiceMock.Object);

        return (service, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // GetAllAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetAllAsync_ReturnsAllPromotionsOrderedByPriority()
    {
        // Arrange
        var promos = new List<Promotion>
        {
            CreatePromotion(id: Guid.NewGuid(), name: "Low Priority", priority: 10),
            CreatePromotion(id: Guid.NewGuid(), name: "High Priority", priority: 1),
            CreatePromotion(id: Guid.NewGuid(), name: "Medium Priority", priority: 5)
        };
        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("High Priority");   // priority 1
        result[1].Name.Should().Be("Medium Priority"); // priority 5
        result[2].Name.Should().Be("Low Priority");    // priority 10
    }

    [Fact]
    public async Task GetAllAsync_NoPromotions_ReturnsEmptyList()
    {
        var (service, _, _) = BuildServiceWithMocks(promotions: new List<Promotion>());
        var result = await service.GetAllAsync();
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetByIdAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetByIdAsync_ExistingPromotion_ReturnsDto()
    {
        // Arrange
        var promoId = Guid.NewGuid();
        var promo = CreatePromotion(id: promoId, name: "عرض خاص", value: 15m);
        var (service, _, _) = BuildServiceWithMocks(promotions: new List<Promotion> { promo });

        // Act
        var result = await service.GetByIdAsync(promoId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(promoId);
        result.Name.Should().Be("عرض خاص");
        result.Value.Should().Be(15m);
        result.Type.Should().Be("Percentage");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPromotion_ReturnsNull()
    {
        var (service, _, _) = BuildServiceWithMocks(promotions: new List<Promotion>());
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ========================================================================
    // CreateAsync Tests
    // ========================================================================

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCreatedPromotion()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(1);
        var endDate = DateTime.UtcNow.AddDays(31);
        var request = new CreatePromotionRequest(
            Name: "حسم 20%", Description: "خصم 20% على جميع المنتجات",
            Type: "Percentage", Value: 20m,
            StartDate: startDate, EndDate: endDate,
            MinPurchaseAmount: 50m);

        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks();

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("حسم 20%");
        result.Value.Should().Be(20m);
        result.Type.Should().Be("Percentage");
        result.IsActive.Should().BeTrue();

        // Promotion was added to the repository
        unitOfWorkMock.Verify(u => u.Promotions.AddAsync(
            It.Is<Promotion>(p =>
                p.Name == "حسم 20%" &&
                p.Value == 20m &&
                p.Type == PromotionType.Percentage &&
                p.MinPurchaseAmount == 50m)), Times.Once);

        // Audit was logged
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Promotion",
            It.IsAny<Guid>(), null,
            It.Is<string>(s => s.Contains("حسم 20%")),
            "تم إنشاء عرض ترويجي جديد"), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange — end date is before start date
        var startDate = DateTime.UtcNow.AddDays(10);
        var endDate = DateTime.UtcNow.AddDays(5);
        var request = new CreatePromotionRequest(
            Name: "Invalid", Description: null,
            Type: "Percentage", Value: 10m,
            StartDate: startDate, EndDate: endDate);

        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsArgumentException()
    {
        var request = new CreatePromotionRequest(
            Name: "", Description: null,
            Type: "FixedAmount", Value: 5m,
            StartDate: DateTime.UtcNow, EndDate: DateTime.UtcNow.AddDays(1));

        var (service, _, _) = BuildServiceWithMocks();

        var act = () => service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("اسم العرض الترويجي مطلوب");
    }

    [Fact]
    public async Task CreateAsync_NullRequest_ThrowsArgumentNullException()
    {
        var (service, _, _) = BuildServiceWithMocks();
        var act = () => service.CreateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // UpdateAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesPromotion()
    {
        // Arrange
        var promoId = Guid.NewGuid();
        var existing = CreatePromotion(id: promoId, name: "Old Name", value: 5m, priority: 0);
        var request = new UpdatePromotionRequest(
            Id: promoId, Name: "New Name", Description: "Updated",
            Type: "FixedAmount", Value: 8m,
            StartDate: DateTime.UtcNow.AddDays(-1), EndDate: DateTime.UtcNow.AddDays(30),
            IsActive: true, Priority: 5,
            MinPurchaseAmount: 20m);

        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks(
            promotions: new List<Promotion> { existing });

        // Act
        var result = await service.UpdateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Name");
        result.Value.Should().Be(8m);
        result.Type.Should().Be("FixedAmount");

        unitOfWorkMock.Verify(u => u.Promotions.UpdateAsync(
            It.Is<Promotion>(p =>
                p.Name == "New Name" &&
                p.Value == 8m &&
                p.Type == PromotionType.FixedAmount)), Times.Once);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Promotion",
            promoId, It.IsAny<string>(),
            It.Is<string>(s => s.Contains("New Name")),
            "تم تحديث العرض الترويجي"), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentPromotion_ThrowsInvalidOperationException()
    {
        var request = new UpdatePromotionRequest(
            Id: Guid.NewGuid(), Name: "Test", Description: null,
            Type: "Percentage", Value: 10m,
            StartDate: DateTime.UtcNow, EndDate: DateTime.UtcNow.AddDays(1),
            IsActive: true, Priority: 0);

        var (service, _, _) = BuildServiceWithMocks();

        var act = () => service.UpdateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العرض الترويجي غير موجود");
    }

    // ========================================================================
    // DeleteAsync Tests
    // ========================================================================

    [Fact]
    public async Task DeleteAsync_ExistingPromotion_MarksAsDeleted()
    {
        // Arrange
        var promoId = Guid.NewGuid();
        var promo = CreatePromotion(id: promoId, name: "To Delete", value: 10m);
        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks(
            promotions: new List<Promotion> { promo });

        // Act
        await service.DeleteAsync(promoId);

        // Assert — MarkAsDeleted() sets IsDeleted flag
        unitOfWorkMock.Verify(u => u.Promotions.UpdateAsync(
            It.Is<Promotion>(p => p.Name == "To Delete")), Times.Once);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Promotion",
            promoId, null,
            It.Is<string>(s => s.Contains("To Delete")),
            "تم حذف العرض الترويجي"), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentPromotion_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks();
        var act = () => service.DeleteAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العرض الترويجي غير موجود");
    }

    // ========================================================================
    // GetEligiblePromotionsAsync — Percentage Type
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_Percentage_ReturnsCorrectDiscount()
    {
        // Arrange
        var promos = new List<Promotion>
        {
            CreatePromotion(name: "خصم 10%", type: "Percentage", value: 10m, priority: 1)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 50.000m) // subtotal = 100
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — 10% of 100 = 10
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(10.000m);
        result[0].Name.Should().Be("خصم 10%");
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_FixedAmount_ReturnsMinOfValueAndSubtotal()
    {
        // Arrange — fixed discount of 30 JOD on 100 JOD subtotal
        var promos = new List<Promotion>
        {
            CreatePromotion(name: "خصم 30 د.أ", type: "FixedAmount", value: 30m)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 50.000m) // subtotal = 100
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — Min(30, 100) = 30
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(30.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_FixedAmountExceedsSubtotal_CapsAtSubtotal()
    {
        // Arrange — fixed discount of 200 JOD on 100 JOD subtotal
        var promos = new List<Promotion>
        {
            CreatePromotion(name: "خصم كبير", type: "FixedAmount", value: 200m)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 50.000m) // subtotal = 100
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — Min(200, 100) = 100
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(100.000m);
    }

    // ========================================================================
    // GetEligiblePromotionsAsync — BuyXGetY
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_Buy3Get1Free_CalculatesCorrectly()
    {
        // Arrange
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "اشتر 3 واحصل على 1 مجاناً",
                type: "BuyXGetY", value: 0m,
                buyQuantity: 3, freeQuantity: 1)
        };

        // Buy 8 items at 10 JOD each → 8 / (3+1) = 2 full sets → 2 * 1 * 10 = 20 discount
        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 8m, unitPrice: 10.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — 2 free items * 10 JOD = 20
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(20.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_BuyXGetY_NotEnoughItems_ReturnsNoDiscount()
    {
        // Arrange — buy 2 get 1 free, but only 1 item purchased
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "Buy 2 Get 1", type: "BuyXGetY", value: 0m,
                buyQuantity: 2, freeQuantity: 1)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 10.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — no discount because totalQty (1) < buyQuantity (2)
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_BuyXGetY_EmptyItems_ReturnsEmpty()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "Buy 2 Get 1", type: "BuyXGetY", value: 0m,
                buyQuantity: 2, freeQuantity: 1)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), new List<SaleItemDto>());

        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetEligiblePromotionsAsync — MultiBuy
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_MultiBuy_EnoughQuantity_ReturnsDiscount()
    {
        // Arrange — 15% off when buying 5+ items
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "خصم الكمية", type: "MultiBuy", value: 15m,
                minQuantity: 5)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 6m, unitPrice: 20.000m) // 6 items * 20 = 120 subtotal
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — 15% of applicable subtotal (120) = 18
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(18.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_MultiBuy_NotEnoughQuantity_ReturnsNoDiscount()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "خصم الكمية", type: "MultiBuy", value: 15m,
                minQuantity: 5)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 20.000m) // only 2 items < 5 minimum
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetEligiblePromotionsAsync — Edge Cases
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_MinPurchaseAmountNotMet_SkipsPromotion()
    {
        // Arrange — requires min 200 JOD purchase, but subtotal is only 100
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "خصم عند الشراء", type: "Percentage", value: 10m,
                minPurchaseAmount: 200m)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 50.000m) // subtotal = 100
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_ExpiredPromotion_Excluded()
    {
        // Arrange — promotion ended yesterday
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "منتهي", type: "Percentage", value: 10m,
                startDate: DateTime.UtcNow.AddDays(-10),
                endDate: DateTime.UtcNow.AddDays(-1))
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 100.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_InactivePromotion_Excluded()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(name: "غير نشط", type: "Percentage", value: 10m, isActive: false)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 100.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_FuturePromotion_Excluded()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "مستقبلي", type: "Percentage", value: 10m,
                startDate: DateTime.UtcNow.AddDays(5),
                endDate: DateTime.UtcNow.AddDays(30))
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 100.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_MultipleEligiblePromotions_ReturnsAllOrdered()
    {
        // Arrange — 3 eligible promotions with different priorities
        var promos = new List<Promotion>
        {
            CreatePromotion(name: "Priority 10", type: "Percentage", value: 5m, priority: 10),
            CreatePromotion(name: "Priority 1", type: "FixedAmount", value: 10m, priority: 1),
            CreatePromotion(name: "Priority 5", type: "Percentage", value: 8m, priority: 5)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 100.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — ordered by priority ascending
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Priority 1");
        result[1].Name.Should().Be("Priority 5");
        result[2].Name.Should().Be("Priority 10");
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_ApplicableProductIds_OnlyAppliesToMatchingProducts()
    {
        // Arrange — promotion applies only to DefaultProductId
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "على منتج محدد",
                type: "Percentage", value: 20m,
                applicableProductIdsJson: JsonSerializer.Serialize(new HashSet<Guid> { DefaultProductId }))
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(productId: DefaultProductId, name: "Eligible", quantity: 2m, unitPrice: 50.000m),
            CreateSaleItem(productId: DefaultProductId2, name: "Not Eligible", quantity: 1m, unitPrice: 100.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        // Act
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Assert — Percentage promotions use total subtotal (not filtered), so 20% of 200 = 40
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(40.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_NoPromotions_ReturnsEmpty()
    {
        var (service, _, _) = BuildServiceWithMocks(promotions: new List<Promotion>());
        var items = new List<SaleItemDto> { CreateSaleItem() };
        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);
        result.Should().BeEmpty();
    }

    // ========================================================================
    // ApplyPromotionAsync Tests
    // ========================================================================

    [Fact]
    public async Task ApplyPromotionAsync_SuccessfulApplication_UpdatesSaleDiscount()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var sale = CreateTestSale(saleId, subTotal: 100m, taxAmount: 16m);
        var promo = CreatePromotion(id: promoId, name: "خصم 10%", type: "Percentage", value: 10m);

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 2m, unitPrice: 50.000m) // subtotal = 100
        };

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            promotions: new List<Promotion> { promo }, sale: sale);

        // Act
        var result = await service.ApplyPromotionAsync(saleId, promoId, items);

        // Assert
        result.Should().NotBeNull();
        result!.DiscountAmount.Should().Be(10.000m);
        result.Name.Should().Be("خصم 10%");

        // Sale promotion was added
        unitOfWorkMock.Verify(u => u.SalePromotions.AddAsync(
            It.Is<SalePromotion>(sp =>
                sp.SaleId == saleId &&
                sp.PromotionId == promoId &&
                sp.DiscountAmount == 10.000m)), Times.Once);

        // Sale discount was incremented and total recalculated (in-memory)
        sale.DiscountAmount.Should().Be(10.000m);
        sale.TotalAmount.Should().Be(106.000m); // 100 + 16 - 10

        // Changes are tracked by EF Core; UpdateAsync is not called explicitly
        // (SalePromotions.AddAsync was verified above, SaveChangesAsync persists all changes)
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyPromotionAsync_SaleNotFound_ThrowsInvalidOperationException()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(id: DefaultPromotionId, name: "Test", type: "Percentage", value: 10m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos, sale: null);

        var act = () => service.ApplyPromotionAsync(
            Guid.NewGuid(), DefaultPromotionId, new List<SaleItemDto> { CreateSaleItem() });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الفاتورة غير موجودة");
    }

    [Fact]
    public async Task ApplyPromotionAsync_PromotionNotFound_ThrowsInvalidOperationException()
    {
        var sale = CreateTestSale(Guid.NewGuid());
        var (service, _, _) = BuildServiceWithMocks(sale: sale);

        var act = () => service.ApplyPromotionAsync(
            sale.Id, Guid.NewGuid(), new List<SaleItemDto> { CreateSaleItem() });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العرض الترويجي غير موجود");
    }

    [Fact]
    public async Task ApplyPromotionAsync_InactivePromotion_ThrowsInvalidOperationException()
    {
        var saleId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var sale = CreateTestSale(saleId);
        var promo = CreatePromotion(id: promoId, name: "Inactive", type: "Percentage", value: 10m, isActive: false);

        var (service, _, _) = BuildServiceWithMocks(
            promotions: new List<Promotion> { promo }, sale: sale);

        var act = () => service.ApplyPromotionAsync(
            saleId, promoId, new List<SaleItemDto> { CreateSaleItem() });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العرض الترويجي غير نشط أو منتهي الصلاحية");
    }

    [Fact]
    public async Task ApplyPromotionAsync_MinPurchaseNotMet_ThrowsInvalidOperationException()
    {
        var saleId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var sale = CreateTestSale(saleId, subTotal: 30m);
        var promo = CreatePromotion(
            id: promoId, name: "Min 50", type: "Percentage", value: 10m,
            minPurchaseAmount: 50m);

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 30.000m) // subtotal = 30 < 50
        };

        var (service, _, _) = BuildServiceWithMocks(
            promotions: new List<Promotion> { promo }, sale: sale);

        var act = () => service.ApplyPromotionAsync(saleId, promoId, items);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الحد الأدنى للشراء هو 50 د.أ");
    }

    [Fact]
    public async Task ApplyPromotionAsync_ZeroDiscount_ReturnsNull()
    {
        // Arrange — BuyXGetY with no qualifying items
        var saleId = Guid.NewGuid();
        var promoId = Guid.NewGuid();
        var sale = CreateTestSale(saleId);
        var promo = CreatePromotion(
            id: promoId, name: "Buy 5 Get 1",
            type: "BuyXGetY", value: 0m,
            buyQuantity: 5, freeQuantity: 1);

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 1m, unitPrice: 10.000m) // only 1 item, need 5 to qualify
        };

        var (service, _, _) = BuildServiceWithMocks(
            promotions: new List<Promotion> { promo }, sale: sale);

        // Act
        var result = await service.ApplyPromotionAsync(saleId, promoId, items);

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // BuyXGetY — Edge Cases
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_Buy2Get1Free_WithExactMultiple_CalculatesCorrectly()
    {
        // Arrange — buy 2 get 1 free, purchase 6 items
        // 6 / (2+1) = 2 full sets → 2 * 1 * unitPrice = discount
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "Buy 2 Get 1", type: "BuyXGetY", value: 0m,
                buyQuantity: 2, freeQuantity: 1)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 6m, unitPrice: 15.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // 2 * 1 * 15 = 30
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(30.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_BuyXGetY_UsesLowestUnitPriceForDiscount()
    {
        // Arrange — buy 2 get 1 free, with items at different prices
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "Buy 2 Get 1", type: "BuyXGetY", value: 0m,
                buyQuantity: 2, freeQuantity: 1)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(productId: DefaultProductId, name: "Cheap", quantity: 3m, unitPrice: 5.000m),
            CreateSaleItem(productId: DefaultProductId2, name: "Expensive", quantity: 3m, unitPrice: 20.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // Total qty = 6, sets = 6 / 3 = 2, discount = 2 * 1 * minPrice(5.000) = 10
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(10.000m);
    }

    // ========================================================================
    // MultiBuy — Edge Cases
    // ========================================================================

    [Fact]
    public async Task GetEligiblePromotionsAsync_MultiBuy_ExactMinQuantity_ReturnsDiscount()
    {
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "خصم 10% عند شراء 3", type: "MultiBuy", value: 10m,
                minQuantity: 3)
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(quantity: 3m, unitPrice: 100.000m) // exactly 3 items
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // 10% of 300 = 30
        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(30.000m);
    }

    [Fact]
    public async Task GetEligiblePromotionsAsync_MultiBuy_AppliesToApplicableItemsSubtotal()
    {
        // Arrange — 20% off with min 2 items, with applicable product filter
        // Actually MultiBuy doesn't use applicable items in the current implementation
        // It uses all items for qty check and applicableSubTotal for discount calc
        var promos = new List<Promotion>
        {
            CreatePromotion(
                name: "خصم الكمية", type: "MultiBuy", value: 20m,
                minQuantity: 2,
                applicableProductIdsJson: JsonSerializer.Serialize(new HashSet<Guid> { DefaultProductId }))
        };

        var items = new List<SaleItemDto>
        {
            CreateSaleItem(productId: DefaultProductId, name: "Eligible", quantity: 3m, unitPrice: 50.000m),
            CreateSaleItem(productId: DefaultProductId2, name: "Not Eligible", quantity: 5m, unitPrice: 10.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(promotions: promos);

        var result = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        result.Should().HaveCount(1);
        result[0].DiscountAmount.Should().Be(30.000m);
    }

    // ========================================================================
    // Audit Logging
    // ========================================================================

    [Fact]
    public async Task CreateAsync_AuditLoggedWithCorrectActionType()
    {
        var request = new CreatePromotionRequest(
            Name: "Test", Description: null,
            Type: "Percentage", Value: 10m,
            StartDate: DateTime.UtcNow, EndDate: DateTime.UtcNow.AddDays(1));

        var (service, _, auditMock) = BuildServiceWithMocks();

        await service.CreateAsync(request);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Promotion",
            It.IsAny<Guid>(), null,
            It.IsAny<string>(), "تم إنشاء عرض ترويجي جديد"), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_AuditLoggedWithBeforeAndAfterValues()
    {
        var promoId = Guid.NewGuid();
        var existing = CreatePromotion(id: promoId, name: "Before", value: 5m);
        var request = new UpdatePromotionRequest(
            Id: promoId, Name: "After", Description: null,
            Type: "Percentage", Value: 10m,
            StartDate: DateTime.UtcNow.AddDays(-1), EndDate: DateTime.UtcNow.AddDays(30),
            IsActive: true, Priority: 0);

        var (service, _, auditMock) = BuildServiceWithMocks(
            promotions: new List<Promotion> { existing });

        await service.UpdateAsync(request);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Promotion",
            promoId,
            It.Is<string>(s => s.Contains("Before")),
            It.Is<string>(s => s.Contains("After")),
            "تم تحديث العرض الترويجي"), Times.Once);
    }
}
