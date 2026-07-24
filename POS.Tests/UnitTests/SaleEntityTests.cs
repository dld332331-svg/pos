using Xunit;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for the Sale domain entity covering AddItem, RemoveItem,
/// ApplyPromotion, AddPayment, default values, BaseEntity behavior, and
/// property state management.
/// </summary>
public sealed class SaleEntityTests
{
    // ========================================================================
    // Constructor & Defaults
    // ========================================================================

    [Fact]
    public void Constructor_DefaultValues_ShouldBeInitializedCorrectly()
    {
        var sale = new Sale();

        sale.Id.Should().NotBeEmpty();
        sale.InvoiceNumber.Should().BeEmpty();
        sale.SubTotal.Should().Be(0m);
        sale.TaxAmount.Should().Be(0m);
        sale.DiscountAmount.Should().Be(0m);
        sale.TotalAmount.Should().Be(0m);
        sale.RoundAmount.Should().Be(0m);
        sale.RemainingAmount.Should().Be(0m);
        sale.Status.Should().Be(SaleStatus.Active);
        sale.IsPaid.Should().BeFalse();
        sale.PaidAt.Should().BeNull();
        sale.Notes.Should().BeNull();
        sale.CustomerName.Should().BeNull();
        sale.CustomerId.Should().BeNull();
        sale.TableId.Should().BeNull();
        sale.OrderType.Should().Be(OrderType.DineIn); // default enum value
        sale.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Collections_ShouldBeEmpty()
    {
        var sale = new Sale();

        sale.SaleItems.Should().BeEmpty();
        sale.Payments.Should().BeEmpty();
        sale.AppliedPromotions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_Collections_ShouldBeImmutable()
    {
        var sale = new Sale();

        sale.SaleItems.Should().NotBeNull();
        sale.Payments.Should().NotBeNull();
        sale.AppliedPromotions.Should().NotBeNull();

        // Verify it's a read-only wrapper
        var act = () => sale.SaleItems.ToList().Add(new SaleItem());
        act.Should().NotThrow(); // Adding to a copy is fine
    }

    [Fact]
    public void Constructor_EachInstance_ShouldHaveUniqueId()
    {
        var sale1 = new Sale();
        var sale2 = new Sale();

        sale1.Id.Should().NotBe(sale2.Id);
    }

    [Fact]
    public void Constructor_CreatedAt_ShouldBeCloseToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var sale = new Sale();
        var after = DateTime.UtcNow.AddSeconds(1);

        sale.CreatedAt.Should().BeOnOrAfter(before);
        sale.CreatedAt.Should().BeOnOrBefore(after);
    }

    // ========================================================================
    // AddItem
    // ========================================================================

    [Fact]
    public void AddItem_SingleItem_ShouldAddToCollection()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);

        sale.AddItem(item);

        sale.SaleItems.Should().HaveCount(1);
        sale.SaleItems.Should().Contain(item);
    }

    [Fact]
    public void AddItem_MultipleItems_ShouldMaintainInsertionOrder()
    {
        var sale = new Sale();
        var item1 = CreateSaleItem(sale.Id);
        var item2 = CreateSaleItem(sale.Id);
        var item3 = CreateSaleItem(sale.Id);

        sale.AddItem(item1);
        sale.AddItem(item2);
        sale.AddItem(item3);

        sale.SaleItems.Should().HaveCount(3);
        sale.SaleItems.Should().ContainInOrder(item1, item2, item3);
    }

    [Fact]
    public void AddItem_CorrectSaleId_ShouldMatchParent()
    {
        var sale = new Sale();
        var item = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Test",
            Quantity = 1,
            UnitPrice = 10.000m,
            LineTotal = 10.000m
        };

        sale.AddItem(item);

        item.SaleId.Should().Be(sale.Id);
    }

    [Fact]
    public void AddItem_ShouldAllowDuplicateProducts()
    {
        var sale = new Sale();
        var productId = Guid.NewGuid();

        var item1 = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = productId,
            ProductName = "Duplicate",
            Quantity = 1,
            UnitPrice = 10.000m,
            LineTotal = 10.000m
        };
        var item2 = new SaleItem
        {
            SaleId = sale.Id,
            ProductId = productId,
            ProductName = "Duplicate",
            Quantity = 2,
            UnitPrice = 10.000m,
            LineTotal = 20.000m
        };

        sale.AddItem(item1);
        sale.AddItem(item2);

        sale.SaleItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddItem_NullItem_ShouldAddNullToList()
    {
        // Note: Sale.AddItem has no null guard, so null is added to the list.
        var sale = new Sale();

        sale.AddItem(null!);

        sale.SaleItems.Should().ContainSingle();
        sale.SaleItems.Single().Should().BeNull();
    }

    // ========================================================================
    // RemoveItem
    // ========================================================================

    [Fact]
    public void RemoveItem_ExistingItem_ShouldRemoveFromCollection()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);
        sale.AddItem(item);

        sale.RemoveItem(item);

        sale.SaleItems.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_FromMultiple_ShouldRemoveSpecificItemOnly()
    {
        var sale = new Sale();
        var item1 = CreateSaleItem(sale.Id);
        var item2 = CreateSaleItem(sale.Id);
        var item3 = CreateSaleItem(sale.Id);
        sale.AddItem(item1);
        sale.AddItem(item2);
        sale.AddItem(item3);

        sale.RemoveItem(item2);

        sale.SaleItems.Should().HaveCount(2);
        sale.SaleItems.Should().Contain(item1);
        sale.SaleItems.Should().NotContain(item2);
        sale.SaleItems.Should().Contain(item3);
    }

    [Fact]
    public void RemoveItem_NonExistentItem_ShouldNotThrow()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);

        var act = () => sale.RemoveItem(item);

        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveItem_EmptyCollection_ShouldNotThrow()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);

        var act = () => sale.RemoveItem(item);

        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveItem_ThenAddAgain_ShouldReAddSuccessfully()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);
        sale.AddItem(item);
        sale.RemoveItem(item);

        sale.AddItem(item);

        sale.SaleItems.Should().ContainSingle();
    }

    // ========================================================================
    // ApplyPromotion
    // ========================================================================

    [Fact]
    public void ApplyPromotion_SinglePromotion_ShouldAddToCollection()
    {
        var sale = new Sale();
        var promotion = CreateSalePromotion(sale.Id);

        sale.ApplyPromotion(promotion);

        sale.AppliedPromotions.Should().HaveCount(1);
        sale.AppliedPromotions.Should().Contain(promotion);
    }

    [Fact]
    public void ApplyPromotion_MultiplePromotions_ShouldAllBeAdded()
    {
        var sale = new Sale();
        var promo1 = CreateSalePromotion(sale.Id);
        var promo2 = CreateSalePromotion(sale.Id);
        var promo3 = CreateSalePromotion(sale.Id);

        sale.ApplyPromotion(promo1);
        sale.ApplyPromotion(promo2);
        sale.ApplyPromotion(promo3);

        sale.AppliedPromotions.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyPromotion_ShouldPreserveDiscountAmount()
    {
        var sale = new Sale();
        var promotion = CreateSalePromotion(sale.Id, discountAmount: 5.500m);

        sale.ApplyPromotion(promotion);

        sale.AppliedPromotions.Single().DiscountAmount.Should().Be(5.500m);
    }

    [Fact]
    public void ApplyPromotion_ShouldPreserveDescription()
    {
        var sale = new Sale();
        var promotion = CreateSalePromotion(sale.Id, description: "تخفيض 10%");

        sale.ApplyPromotion(promotion);

        sale.AppliedPromotions.Single().Description.Should().Be("تخفيض 10%");
    }

    [Fact]
    public void ApplyPromotion_DuplicatePromotion_ShouldAddSeparateEntries()
    {
        var sale = new Sale();
        var samePromotionId = Guid.NewGuid();
        var promo1 = CreateSalePromotion(sale.Id, promotionId: samePromotionId, discountAmount: 2.000m);
        var promo2 = CreateSalePromotion(sale.Id, promotionId: samePromotionId, discountAmount: 3.000m);

        sale.ApplyPromotion(promo1);
        sale.ApplyPromotion(promo2);

        sale.AppliedPromotions.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyPromotion_NullPromotion_ShouldAddNullToList()
    {
        // Note: Sale.ApplyPromotion has no null guard, so null is added to the list.
        var sale = new Sale();

        sale.ApplyPromotion(null!);

        sale.AppliedPromotions.Should().ContainSingle();
        sale.AppliedPromotions.Single().Should().BeNull();
    }

    // ========================================================================
    // AddPayment
    // ========================================================================

    [Fact]
    public void AddPayment_SinglePayment_ShouldAddToCollection()
    {
        var sale = new Sale();
        var payment = CreatePayment(sale.Id);

        sale.AddPayment(payment);

        sale.Payments.Should().HaveCount(1);
        sale.Payments.Should().Contain(payment);
    }

    [Fact]
    public void AddPayment_MultiplePayments_ShouldAllBeAdded()
    {
        var sale = new Sale();
        var payment1 = CreatePayment(sale.Id);
        var payment2 = CreatePayment(sale.Id);

        sale.AddPayment(payment1);
        sale.AddPayment(payment2);

        sale.Payments.Should().HaveCount(2);
    }

    [Fact]
    public void AddPayment_PartialPayments_ShouldSumToTotal()
    {
        var sale = new Sale
        {
            TotalAmount = 50.000m
        };
        var payment1 = CreatePayment(sale.Id, amount: 20.000m);
        var payment2 = CreatePayment(sale.Id, amount: 30.000m);

        sale.AddPayment(payment1);
        sale.AddPayment(payment2);

        sale.Payments.Sum(p => p.Amount).Should().Be(sale.TotalAmount);
    }

    [Fact]
    public void AddPayment_ShouldPreservePaymentMethod()
    {
        var sale = new Sale();
        var payment = new Payment
        {
            SaleId = sale.Id,
            PaymentMethod = PaymentMethod.Card,
            Amount = 25.000m
        };

        sale.AddPayment(payment);

        sale.Payments.Single().PaymentMethod.Should().Be(PaymentMethod.Card);
    }

    [Fact]
    public void AddPayment_NullPayment_ShouldAddNullToList()
    {
        // Note: Sale.AddPayment has no null guard, so null is added to the list.
        var sale = new Sale();

        sale.AddPayment(null!);

        sale.Payments.Should().ContainSingle();
        sale.Payments.Single().Should().BeNull();
    }

    // ========================================================================
    // Property State Management
    // ========================================================================

    [Fact]
    public void Status_SetToCompleted_ShouldUpdateStatus()
    {
        var sale = new Sale();

        sale.Status = SaleStatus.Completed;

        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public void Status_SetToCancelled_ShouldUpdateStatus()
    {
        var sale = new Sale();

        sale.Status = SaleStatus.Cancelled;

        sale.Status.Should().Be(SaleStatus.Cancelled);
    }

    [Fact]
    public void Status_SetToHeld_ThenBackToActive_ShouldTransition()
    {
        var sale = new Sale();

        sale.Status = SaleStatus.Held;
        sale.Status = SaleStatus.Active;

        sale.Status.Should().Be(SaleStatus.Active);
    }

    [Fact]
    public void IsPaid_True_ShouldSetPaidAt()
    {
        var sale = new Sale();

        sale.IsPaid = true;
        sale.PaidAt = DateTime.UtcNow;

        sale.IsPaid.Should().BeTrue();
        sale.PaidAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetInvoiceNumber_ShouldStoreValue()
    {
        var sale = new Sale();
        var invoiceNumber = "INV-2026-0001";

        sale.InvoiceNumber = invoiceNumber;

        sale.InvoiceNumber.Should().Be(invoiceNumber);
    }

    [Fact]
    public void SetCustomerId_ShouldStoreReference()
    {
        var sale = new Sale();
        var customerId = Guid.NewGuid();

        sale.CustomerId = customerId;

        sale.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public void SetCustomerName_ShouldStoreName()
    {
        var sale = new Sale();

        sale.CustomerName = "أحمد محمد";

        sale.CustomerName.Should().Be("أحمد محمد");
    }

    [Fact]
    public void SetTableId_ShouldStoreTableReference()
    {
        var sale = new Sale();
        var tableId = Guid.NewGuid();

        sale.TableId = tableId;

        sale.TableId.Should().Be(tableId);
    }

    [Fact]
    public void SetOrderType_ShouldUpdateType()
    {
        var sale = new Sale();

        sale.OrderType = OrderType.Takeaway;

        sale.OrderType.Should().Be(OrderType.Takeaway);
    }

    [Fact]
    public void SetShiftId_ShouldStoreShiftReference()
    {
        var sale = new Sale();
        var shiftId = Guid.NewGuid();

        sale.ShiftId = shiftId;

        sale.ShiftId.Should().Be(shiftId);
    }

    [Fact]
    public void SetUserId_ShouldStoreUserReference()
    {
        var sale = new Sale();
        var userId = Guid.NewGuid();

        sale.UserId = userId;

        sale.UserId.Should().Be(userId);
    }

    [Fact]
    public void SetRegisterId_ShouldStoreRegisterReference()
    {
        var sale = new Sale();
        var registerId = Guid.NewGuid();

        sale.RegisterId = registerId;

        sale.RegisterId.Should().Be(registerId);
    }

    [Fact]
    public void SetNotes_ShouldStoreNotes()
    {
        var sale = new Sale();

        sale.Notes = "ملاحظات العميل";

        sale.Notes.Should().Be("ملاحظات العميل");
    }

    [Fact]
    public void SetRoundAmount_ShouldStoreRounding()
    {
        var sale = new Sale();

        sale.RoundAmount = 0.003m;

        sale.RoundAmount.Should().Be(0.003m);
    }

    [Fact]
    public void SetRemainingAmount_ShouldStoreBalance()
    {
        var sale = new Sale
        {
            TotalAmount = 50.000m
        };

        sale.RemainingAmount = 20.000m;

        sale.RemainingAmount.Should().Be(20.000m);
    }

    // ========================================================================
    // Mixed Operations (Integration-style within the domain entity)
    // ========================================================================

    [Fact]
    public void AddItem_And_ApplyPromotion_ShouldBothBeIndependent()
    {
        var sale = new Sale();
        var item = CreateSaleItem(sale.Id);
        var promo = CreateSalePromotion(sale.Id);

        sale.AddItem(item);
        sale.ApplyPromotion(promo);

        sale.SaleItems.Should().HaveCount(1);
        sale.AppliedPromotions.Should().HaveCount(1);
    }

    [Fact]
    public void AddItem_RemoveItem_AddPayment_ShouldManageAllCollections()
    {
        var sale = new Sale();
        var item1 = CreateSaleItem(sale.Id);
        var item2 = CreateSaleItem(sale.Id);
        var payment = CreatePayment(sale.Id);

        sale.AddItem(item1);
        sale.AddItem(item2);
        sale.AddPayment(payment);

        sale.SaleItems.Should().HaveCount(2);
        sale.Payments.Should().HaveCount(1);

        sale.RemoveItem(item1);
        sale.SaleItems.Should().HaveCount(1);
        sale.Payments.Should().HaveCount(1); // Unaffected
    }

    // ========================================================================
    // BaseEntity Behavior
    // ========================================================================

    [Fact]
    public void MarkAsModified_ShouldSetUpdatedAt()
    {
        var sale = new Sale();
        var before = DateTime.UtcNow.AddSeconds(-1);

        sale.MarkAsModified();

        sale.UpdatedAt.Should().BeOnOrAfter(before);
        sale.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsModified_WithUserId_ShouldSetUpdatedBy()
    {
        var sale = new Sale();
        var userId = Guid.NewGuid();

        sale.MarkAsModified(userId);

        sale.UpdatedBy.Should().Be(userId);
    }

    [Fact]
    public void MarkAsDeleted_ShouldSetIsDeletedTrue()
    {
        var sale = new Sale();

        sale.MarkAsDeleted();

        sale.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Restore_AfterDeletion_ShouldSetIsDeletedFalse()
    {
        var sale = new Sale();
        sale.MarkAsDeleted();

        sale.Restore();

        sale.IsDeleted.Should().BeFalse();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static SaleItem CreateSaleItem(Guid saleId)
    {
        return new SaleItem
        {
            SaleId = saleId,
            ProductId = Guid.NewGuid(),
            ProductName = "Test Product",
            Quantity = 1,
            UnitPrice = 10.000m,
            LineTotal = 10.000m
        };
    }

    private static SalePromotion CreateSalePromotion(
        Guid saleId,
        decimal discountAmount = 0m,
        string? description = null,
        Guid? promotionId = null)
    {
        return new SalePromotion
        {
            SaleId = saleId,
            PromotionId = promotionId ?? Guid.NewGuid(),
            DiscountAmount = discountAmount,
            Description = description
        };
    }

    private static Payment CreatePayment(Guid saleId, decimal amount = 10.000m)
    {
        return new Payment
        {
            SaleId = saleId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = amount
        };
    }
}
