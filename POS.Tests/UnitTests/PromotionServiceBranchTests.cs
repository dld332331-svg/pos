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
/// Targeted unit tests for PromotionService branch gaps:
/// MultiBuy type, unknown promotion type (default switch case),
/// and GetApplicableItems with empty/fallback product IDs.
/// </summary>
public sealed class PromotionServiceBranchTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Promotion CreatePromotion(
        Guid? id = null,
        string name = "عرض",
        string type = "Percentage",
        decimal value = 10m,
        bool isActive = true,
        decimal? minPurchaseAmount = null,
        int? minQuantity = null,
        int? buyQuantity = null,
        int? freeQuantity = null,
        string? applicableProductIdsJson = null)
    {
        return new Promotion
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = name,
            Type = Enum.Parse<PromotionType>(type),
            Value = value,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            IsActive = isActive,
            MinPurchaseAmount = minPurchaseAmount,
            MinQuantity = minQuantity,
            BuyQuantity = buyQuantity,
            FreeQuantity = freeQuantity,
            ApplicableProductIdsJson = applicableProductIdsJson
        };
    }

    private static SaleItemDto CreateSaleItem(Guid? productId = null, decimal quantity = 1m, decimal unitPrice = 10.000m)
    {
        return new SaleItemDto(
            Guid.NewGuid(),
            productId ?? Guid.NewGuid(),
            "Test",
            quantity,
            unitPrice,
            0, 0.16m, 0, 0, 5.000m, null, null);
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

    private (PromotionService service, Mock<IUnitOfWork> uowMock)
        BuildServiceWithMocks(List<Promotion>? promotions = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        auditMock.Setup(a => a.LogAsync(
            It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
            It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var promoRepoMock = new Mock<IRepository<Promotion>>();
        promoRepoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(promotions ?? new List<Promotion>());
        uowMock.Setup(u => u.Promotions).Returns(promoRepoMock.Object);

        // Stub remaining repos
        uowMock.Setup(u => u.Sales).Returns(CreateEmptyRepoMock<Sale>().Object);
        uowMock.Setup(u => u.SalePromotions).Returns(CreateEmptyRepoMock<SalePromotion>().Object);
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        uowMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        uowMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.InventoryBatches).Returns(CreateEmptyRepoMock<InventoryBatch>().Object);
        uowMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        uowMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        uowMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);

        var service = new PromotionService(uowMock.Object, auditMock.Object);
        return (service, uowMock);
    }

    // ========================================================================
    // MultiBuy — Eligible Path
    // ========================================================================

    [Fact]
    public async Task GetEligible_MultiBuy_Eligible_ReturnsDiscount()
    {
        var promo = CreatePromotion(type: "MultiBuy", value: 10m, minQuantity: 3);
        var items = new List<SaleItemDto> { CreateSaleItem(quantity: 5m, unitPrice: 10.000m) };

        var (service, _) = BuildServiceWithMocks(new List<Promotion> { promo });

        var results = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // totalQty=5 >= minQuantity=3 → discount = 50 * 10% = 5.000
        results.Should().ContainSingle();
        results[0].DiscountAmount.Should().Be(5.000m);
    }

    // ========================================================================
    // MultiBuy — Not Enough Quantity
    // ========================================================================

    [Fact]
    public async Task GetEligible_MultiBuy_NotEnoughQty_ReturnsEmpty()
    {
        var promo = CreatePromotion(type: "MultiBuy", value: 10m, minQuantity: 5);
        var items = new List<SaleItemDto> { CreateSaleItem(quantity: 2m, unitPrice: 10.000m) };

        var (service, _) = BuildServiceWithMocks(new List<Promotion> { promo });

        var results = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        results.Should().BeEmpty();
    }

    // ========================================================================
    // Unknown Promotion Type — Default Switch Case
    // ========================================================================

    [Fact]
    public async Task GetEligible_UnknownPromotionType_ReturnsEmpty()
    {
        // Use int-cast enum value that doesn't match any named case
        var promo = CreatePromotion(type: ((int)(PromotionType)999).ToString(), value: 10m);
        var items = new List<SaleItemDto> { CreateSaleItem(quantity: 1m, unitPrice: 10.000m) };

        var (service, _) = BuildServiceWithMocks(new List<Promotion> { promo });

        // Unknown type → switch default `_ => 0` → discount = 0 → not returned
        var results = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        results.Should().BeEmpty();
    }

    // ========================================================================
    // Applicable Product IDs — No Match
    // ========================================================================

    [Fact]
    public async Task GetEligible_ApplicableProductIds_NoMatch_ReturnsEmpty()
    {
        // Use BuyXGetY type so applicableItems count matters for CalculateBuyXGetY
        var targetId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new[] { targetId });
        var promo = CreatePromotion(
            type: "BuyXGetY",
            value: 0m,
            buyQuantity: 2,
            freeQuantity: 1,
            applicableProductIdsJson: json);
        // Item has different ProductId → GetApplicableItems returns empty list
        // CalculateBuyXGetY with empty items → items.Count == 0 → returns 0
        var items = new List<SaleItemDto> { CreateSaleItem(productId: Guid.NewGuid(), quantity: 5m, unitPrice: 10.000m) };

        var (service, _) = BuildServiceWithMocks(new List<Promotion> { promo });

        var results = await service.GetEligiblePromotionsAsync(Guid.NewGuid(), items);

        // No matching items → CalculateBuyXGetY returns 0 → discount = 0 → not returned
        results.Should().BeEmpty();
    }
}
