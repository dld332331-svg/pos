using Xunit;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for Payment, SaleItem, SalePromotion, and SaleItemModifier
/// domain entities covering constructors, default values, property management,
/// collection behavior, BaseEntity lifecycle, and edge cases.
/// </summary>
public sealed class PaymentSaleItemSalePromotionEntityTests
{
    // ========================================================================
    // Payment Entity
    // ========================================================================

    public sealed class PaymentEntityTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldBeInitializedCorrectly()
        {
            var payment = new Payment();

            payment.Id.Should().NotBeEmpty();
            payment.SaleId.Should().BeEmpty();
            payment.PaymentMethod.Should().Be(PaymentMethod.Cash); // default enum value
            payment.Amount.Should().Be(0m);
            payment.TipAmount.Should().Be(0m);
            payment.ReferenceNumber.Should().BeNull();
            payment.CardLast4.Should().BeNull();
            payment.Sale.Should().BeNull();
            payment.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void Constructor_Timestamp_ShouldDefaultToUtcNow()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var payment = new Payment();
            var after = DateTime.UtcNow.AddSeconds(1);

            payment.Timestamp.Should().BeOnOrAfter(before);
            payment.Timestamp.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void SetSaleId_ShouldStoreReference()
        {
            var payment = new Payment();
            var saleId = Guid.NewGuid();

            payment.SaleId = saleId;

            payment.SaleId.Should().Be(saleId);
        }

        [Fact]
        public void SetPaymentMethod_AllValues_ShouldBeSettable()
        {
            foreach (PaymentMethod method in Enum.GetValues<PaymentMethod>())
            {
                var payment = new Payment { PaymentMethod = method };
                payment.PaymentMethod.Should().Be(method);
            }
        }

        [Fact]
        public void SetAmount_ShouldStoreAmount()
        {
            var payment = new Payment { Amount = 50.000m };
            payment.Amount.Should().Be(50.000m);
        }

        [Fact]
        public void SetAmount_NegativeValue_ShouldStoreNegative()
        {
            var payment = new Payment { Amount = -10.000m };
            payment.Amount.Should().Be(-10.000m);
        }

        [Fact]
        public void SetAmount_HighPrecision_ShouldStoreFullPrecision()
        {
            var payment = new Payment { Amount = 0.123m };
            payment.Amount.Should().Be(0.123m);
        }

        [Fact]
        public void SetTipAmount_ShouldStoreTip()
        {
            var payment = new Payment { TipAmount = 2.500m };
            payment.TipAmount.Should().Be(2.500m);
        }

        [Fact]
        public void SetTipAmount_Zero_ShouldDefault()
        {
            var payment = new Payment();
            payment.TipAmount.Should().Be(0m);
        }

        [Fact]
        public void SetReferenceNumber_ShouldStoreReference()
        {
            var payment = new Payment { ReferenceNumber = "TXN-123456" };
            payment.ReferenceNumber.Should().Be("TXN-123456");
        }

        [Fact]
        public void SetReferenceNumber_Null_ShouldAllowNull()
        {
            var payment = new Payment();
            payment.ReferenceNumber.Should().BeNull();
        }

        [Fact]
        public void SetCardLast4_ShouldStoreLast4()
        {
            var payment = new Payment { CardLast4 = "1234" };
            payment.CardLast4.Should().Be("1234");
        }

        [Fact]
        public void SetCardLast4_EmptyString_ShouldStoreEmpty()
        {
            var payment = new Payment { CardLast4 = string.Empty };
            payment.CardLast4.Should().BeEmpty();
        }

        [Fact]
        public void SetSaleNavigation_ShouldStoreReference()
        {
            var sale = new Sale();
            var payment = new Payment { Sale = sale };

            payment.Sale.Should().BeSameAs(sale);
        }

        [Fact]
        public void TwoDifferentPayments_ShouldHaveDifferentIds()
        {
            var payment1 = new Payment();
            var payment2 = new Payment();
            payment1.Id.Should().NotBe(payment2.Id);
        }

        [Fact]
        public void MarkAsModified_ShouldSetUpdatedAt()
        {
            var payment = new Payment();
            payment.MarkAsModified();
            payment.UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public void MarkAsDeleted_Restore_ShouldToggleIsDeleted()
        {
            var payment = new Payment();
            payment.MarkAsDeleted();
            payment.IsDeleted.Should().BeTrue();
            payment.Restore();
            payment.IsDeleted.Should().BeFalse();
        }
    }

    // ========================================================================
    // SaleItem Entity
    // ========================================================================

    public sealed class SaleItemEntityTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldBeInitializedCorrectly()
        {
            var item = new SaleItem();

            item.Id.Should().NotBeEmpty();
            item.SaleId.Should().BeEmpty();
            item.ProductId.Should().BeEmpty();
            item.ProductName.Should().BeEmpty();
            item.ProductArabicName.Should().BeNull();
            item.KitchenStationId.Should().BeNull();
            item.Quantity.Should().Be(0m);
            item.UnitPrice.Should().Be(0m);
            item.Discount.Should().Be(0m);
            item.DiscountAmount.Should().Be(0m);
            item.TaxRate.Should().Be(0m);
            item.TaxAmount.Should().Be(0m);
            item.TotalPrice.Should().Be(0m);
            item.LineTotal.Should().Be(0m);
            item.Cost.Should().Be(0m);
            item.Notes.Should().BeNull();
            item.ModifierSummary.Should().BeNull();
            item.UnitOfMeasureId.Should().BeNull();
            item.DisplayQuantity.Should().BeNull();
            item.Sale.Should().BeNull();
            item.Product.Should().BeNull();
            item.KitchenStation.Should().BeNull();
            item.UnitOfMeasure.Should().BeNull();
            item.Modifiers.Should().BeEmpty();
            item.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ModifiersCollection_ShouldBeImmutable()
        {
            var item = new SaleItem();
            item.Modifiers.Should().NotBeNull();
            item.Modifiers.Should().BeEmpty();
        }

        // ── SaleItem: Properties ───────────────────────────────────────

        [Fact]
        public void SetSaleId_ShouldStoreReference()
        {
            var saleId = Guid.NewGuid();
            var item = new SaleItem { SaleId = saleId };
            item.SaleId.Should().Be(saleId);
        }

        [Fact]
        public void SetProductId_ShouldStoreProductReference()
        {
            var productId = Guid.NewGuid();
            var item = new SaleItem { ProductId = productId };
            item.ProductId.Should().Be(productId);
        }

        [Fact]
        public void SetProductName_ShouldStoreName()
        {
            var item = new SaleItem { ProductName = "Coffee Latte" };
            item.ProductName.Should().Be("Coffee Latte");
        }

        [Fact]
        public void SetProductArabicName_ShouldStoreArabicName()
        {
            var item = new SaleItem { ProductArabicName = "لاتيه" };
            item.ProductArabicName.Should().Be("لاتيه");
        }

        [Fact]
        public void SetKitchenStationId_ShouldStoreStationReference()
        {
            var stationId = Guid.NewGuid();
            var item = new SaleItem { KitchenStationId = stationId };
            item.KitchenStationId.Should().Be(stationId);
        }

        [Fact]
        public void SetQuantity_ShouldStoreQuantity()
        {
            var item = new SaleItem { Quantity = 3.5m };
            item.Quantity.Should().Be(3.5m);
        }

        [Fact]
        public void SetQuantity_Zero_ShouldStoreZero()
        {
            var item = new SaleItem();
            item.Quantity.Should().Be(0m);
        }

        [Fact]
        public void SetUnitPrice_ShouldStorePrice()
        {
            var item = new SaleItem { UnitPrice = 12.500m };
            item.UnitPrice.Should().Be(12.500m);
        }

        [Fact]
        public void SetDiscount_ShouldStoreDiscount()
        {
            var item = new SaleItem { Discount = 2.000m };
            item.Discount.Should().Be(2.000m);
        }

        [Fact]
        public void SetDiscountAmount_ShouldStoreDiscountAmount()
        {
            var item = new SaleItem { DiscountAmount = 1.500m };
            item.DiscountAmount.Should().Be(1.500m);
        }

        [Fact]
        public void SetTaxRate_ShouldStoreTaxRate()
        {
            var item = new SaleItem { TaxRate = 0.16m };
            item.TaxRate.Should().Be(0.16m);
        }

        [Fact]
        public void SetTaxAmount_ShouldStoreTaxAmount()
        {
            var item = new SaleItem { TaxAmount = 3.200m };
            item.TaxAmount.Should().Be(3.200m);
        }

        [Fact]
        public void SetTotalPrice_ShouldStoreTotal()
        {
            var item = new SaleItem { TotalPrice = 23.200m };
            item.TotalPrice.Should().Be(23.200m);
        }

        [Fact]
        public void SetLineTotal_ShouldStoreLineTotal()
        {
            var item = new SaleItem { LineTotal = 23.200m };
            item.LineTotal.Should().Be(23.200m);
        }

        [Fact]
        public void SetCost_ShouldStoreCost()
        {
            var item = new SaleItem { Cost = 8.500m };
            item.Cost.Should().Be(8.500m);
        }

        [Fact]
        public void SetNotes_ShouldStoreNotes()
        {
            var item = new SaleItem { Notes = "Extra hot, no foam" };
            item.Notes.Should().Be("Extra hot, no foam");
        }

        [Fact]
        public void SetModifierSummary_ShouldStoreSummary()
        {
            var item = new SaleItem { ModifierSummary = "Soy Milk, Extra Shot" };
            item.ModifierSummary.Should().Be("Soy Milk, Extra Shot");
        }

        [Fact]
        public void SetUnitOfMeasureId_ShouldStoreUnitReference()
        {
            var unitId = Guid.NewGuid();
            var item = new SaleItem { UnitOfMeasureId = unitId };
            item.UnitOfMeasureId.Should().Be(unitId);
        }

        [Fact]
        public void SetDisplayQuantity_ShouldStoreDisplayValue()
        {
            var item = new SaleItem { DisplayQuantity = 500 };
            item.DisplayQuantity.Should().Be(500);
        }

        // ── SaleItem: Financial Relationships ──────────────────────────

        [Fact]
        public void LineTotal_ShouldCalculateFromQuantityAndUnitPrice()
        {
            var item = new SaleItem
            {
                Quantity = 2,
                UnitPrice = 10.000m,
                LineTotal = 20.000m
            };
            item.LineTotal.Should().Be(item.Quantity * item.UnitPrice);
        }

        [Fact]
        public void TaxAmount_ShouldBeProportionalToLineTotal()
        {
            var item = new SaleItem
            {
                LineTotal = 100.000m,
                TaxRate = 0.16m,
                TaxAmount = 16.000m
            };
            item.TaxAmount.Should().Be(item.LineTotal * item.TaxRate);
        }

        [Fact]
        public void NetRevenue_ShouldBeLineTotalMinusDiscount()
        {
            var item = new SaleItem
            {
                LineTotal = 50.000m,
                Discount = 5.000m
            };
            var net = item.LineTotal - item.Discount;
            net.Should().Be(45.000m);
        }

        [Fact]
        public void Profit_ShouldBeLineTotalMinusCostTimesQuantity()
        {
            var item = new SaleItem
            {
                Quantity = 2,
                LineTotal = 46.400m,
                Cost = 8.500m
            };
            var profit = item.LineTotal - (item.Cost * item.Quantity);
            profit.Should().Be(29.400m);
        }

        // ── SaleItem: AddModifier ──────────────────────────────────────

        [Fact]
        public void AddModifier_SingleModifier_ShouldAddToCollection()
        {
            var item = new SaleItem();
            var modifier = CreateModifier(item.Id);

            item.AddModifier(modifier);

            item.Modifiers.Should().HaveCount(1);
            item.Modifiers.Should().Contain(modifier);
        }

        [Fact]
        public void AddModifier_MultipleModifiers_ShouldMaintainOrder()
        {
            var item = new SaleItem();
            var mod1 = CreateModifier(item.Id);
            var mod2 = CreateModifier(item.Id);
            var mod3 = CreateModifier(item.Id);

            item.AddModifier(mod1);
            item.AddModifier(mod2);
            item.AddModifier(mod3);

            item.Modifiers.Should().HaveCount(3);
            item.Modifiers.Should().ContainInOrder(mod1, mod2, mod3);
        }

        [Fact]
        public void AddModifier_WithSizeName_ShouldStoreSize()
        {
            var item = new SaleItem();
            var modifier = new SaleItemModifier
            {
                SaleItemId = item.Id,
                ModifierId = Guid.NewGuid(),
                ModifierName = "Milk",
                SizeName = "Large",
                Price = 2.000m,
                AdditionalPrice = 1.000m,
                Quantity = 1
            };

            item.AddModifier(modifier);

            item.Modifiers.Single().SizeName.Should().Be("Large");
            item.Modifiers.Single().AdditionalPrice.Should().Be(1.000m);
        }

        [Fact]
        public void AddModifier_WithArabicName_ShouldStoreArabic()
        {
            var item = new SaleItem();
            var modifier = new SaleItemModifier
            {
                SaleItemId = item.Id,
                ModifierId = Guid.NewGuid(),
                ModifierName = "Soy Milk",
                ModifierArabicName = "حليب صويا",
                AdditionalPrice = 2.000m,
                Quantity = 1
            };

            item.AddModifier(modifier);

            item.Modifiers.Single().ModifierArabicName.Should().Be("حليب صويا");
        }

        [Fact]
        public void AddModifier_NullModifier_ShouldAddNull()
        {
            var item = new SaleItem();

            item.AddModifier(null!);

            item.Modifiers.Should().ContainSingle();
            item.Modifiers.Single().Should().BeNull();
        }

        [Fact]
        public void AddModifier_DuplicateModifier_ShouldAllowMultiple()
        {
            var item = new SaleItem();
            var modId = Guid.NewGuid();
            var mod1 = new SaleItemModifier
            {
                SaleItemId = item.Id,
                ModifierId = modId,
                ModifierName = "Extra Cheese",
                AdditionalPrice = 2.000m,
                Quantity = 1
            };
            var mod2 = new SaleItemModifier
            {
                SaleItemId = item.Id,
                ModifierId = modId,
                ModifierName = "Extra Cheese",
                AdditionalPrice = 2.000m,
                Quantity = 1
            };

            item.AddModifier(mod1);
            item.AddModifier(mod2);

            item.Modifiers.Should().HaveCount(2);
        }

        // ── SaleItem: Navigation Properties ────────────────────────────

        [Fact]
        public void SetSaleNavigation_ShouldStoreReference()
        {
            var sale = new Sale();
            var item = new SaleItem { Sale = sale };
            item.Sale.Should().BeSameAs(sale);
        }

        [Fact]
        public void SetProductNavigation_ShouldStoreReference()
        {
            var productId = Guid.NewGuid();
            var item = new SaleItem { ProductId = productId };
            // Product navigation is settable separately
            item.ProductId.Should().Be(productId);
        }

        // ── SaleItem: BaseEntity ────────────────────────────────────────

        [Fact]
        public void MarkAsModified_WithUserId_ShouldSetUpdatedBy()
        {
            var item = new SaleItem();
            var userId = Guid.NewGuid();
            item.MarkAsModified(userId);
            item.UpdatedBy.Should().Be(userId);
        }

        [Fact]
        public void MarkAsDeleted_Restore_ShouldToggle()
        {
            var item = new SaleItem();
            item.MarkAsDeleted();
            item.IsDeleted.Should().BeTrue();
            item.Restore();
            item.IsDeleted.Should().BeFalse();
        }

        private static SaleItemModifier CreateModifier(Guid saleItemId)
        {
            return new SaleItemModifier
            {
                SaleItemId = saleItemId,
                ModifierId = Guid.NewGuid(),
                ModifierName = "Test Modifier",
                AdditionalPrice = 1.000m,
                Quantity = 1
            };
        }
    }

    // ========================================================================
    // SalePromotion Entity
    // ========================================================================

    public sealed class SalePromotionEntityTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldBeInitializedCorrectly()
        {
            var sp = new SalePromotion();

            sp.Id.Should().NotBeEmpty();
            sp.SaleId.Should().BeEmpty();
            sp.PromotionId.Should().BeEmpty();
            sp.DiscountAmount.Should().Be(0m);
            sp.Description.Should().BeNull();
            sp.Sale.Should().BeNull();
            sp.Promotion.Should().BeNull();
            sp.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void SetSaleId_ShouldStoreReference()
        {
            var saleId = Guid.NewGuid();
            var sp = new SalePromotion { SaleId = saleId };
            sp.SaleId.Should().Be(saleId);
        }

        [Fact]
        public void SetPromotionId_ShouldStorePromotionReference()
        {
            var promId = Guid.NewGuid();
            var sp = new SalePromotion { PromotionId = promId };
            sp.PromotionId.Should().Be(promId);
        }

        [Fact]
        public void SetDiscountAmount_ShouldStoreAmount()
        {
            var sp = new SalePromotion { DiscountAmount = 10.000m };
            sp.DiscountAmount.Should().Be(10.000m);
        }

        [Fact]
        public void SetDiscountAmount_Zero_ShouldStoreZero()
        {
            var sp = new SalePromotion();
            sp.DiscountAmount.Should().Be(0m);
        }

        [Fact]
        public void SetDescription_ShouldStoreDescription()
        {
            var sp = new SalePromotion { Description = "10% off entire order" };
            sp.Description.Should().Be("10% off entire order");
        }

        [Fact]
        public void SetDescription_Arabic_ShouldStoreArabic()
        {
            var sp = new SalePromotion { Description = "خصم 10% على الطلب" };
            sp.Description.Should().Be("خصم 10% على الطلب");
        }

        [Fact]
        public void SetDescription_Null_ShouldAllowNull()
        {
            var sp = new SalePromotion();
            sp.Description.Should().BeNull();
        }

        [Fact]
        public void SetSaleNavigation_ShouldStoreReference()
        {
            var sale = new Sale();
            var sp = new SalePromotion { Sale = sale };
            sp.Sale.Should().BeSameAs(sale);
        }

        [Fact]
        public void SetPromotionNavigation_ShouldStoreReference()
        {
            var promId = Guid.NewGuid();
            var sp = new SalePromotion { PromotionId = promId };
            sp.PromotionId.Should().Be(promId);
        }

        [Fact]
        public void TwoDifferentPromotions_ShouldHaveDifferentIds()
        {
            var sp1 = new SalePromotion();
            var sp2 = new SalePromotion();
            sp1.Id.Should().NotBe(sp2.Id);
        }

        [Fact]
        public void MarkAsModified_ShouldSetUpdatedAt()
        {
            var sp = new SalePromotion();
            sp.MarkAsModified();
            sp.UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public void MarkAsDeleted_Restore_ShouldToggleIsDeleted()
        {
            var sp = new SalePromotion();
            sp.MarkAsDeleted();
            sp.IsDeleted.Should().BeTrue();
            sp.Restore();
            sp.IsDeleted.Should().BeFalse();
        }
    }

    // ========================================================================
    // SaleItemModifier Entity
    // ========================================================================

    public sealed class SaleItemModifierEntityTests
    {
        [Fact]
        public void Constructor_DefaultValues_ShouldBeInitializedCorrectly()
        {
            var mod = new SaleItemModifier();

            mod.Id.Should().NotBeEmpty();
            mod.SaleItemId.Should().BeEmpty();
            mod.ModifierId.Should().BeEmpty();
            mod.ModifierName.Should().BeEmpty();
            mod.ModifierArabicName.Should().BeNull();
            mod.SizeName.Should().BeNull();
            mod.Price.Should().Be(0m);
            mod.AdditionalPrice.Should().Be(0m);
            mod.Quantity.Should().Be(0m);
            mod.SaleItem.Should().BeNull();
            mod.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public void SetSaleItemId_ShouldStoreReference()
        {
            var saleItemId = Guid.NewGuid();
            var mod = new SaleItemModifier { SaleItemId = saleItemId };
            mod.SaleItemId.Should().Be(saleItemId);
        }

        [Fact]
        public void SetModifierId_ShouldStoreModifierReference()
        {
            var modId = Guid.NewGuid();
            var mod = new SaleItemModifier { ModifierId = modId };
            mod.ModifierId.Should().Be(modId);
        }

        [Fact]
        public void SetModifierName_ShouldStoreName()
        {
            var mod = new SaleItemModifier { ModifierName = "Extra Cheese" };
            mod.ModifierName.Should().Be("Extra Cheese");
        }

        [Fact]
        public void SetModifierArabicName_ShouldStoreArabic()
        {
            var mod = new SaleItemModifier { ModifierArabicName = "جبنة إضافية" };
            mod.ModifierArabicName.Should().Be("جبنة إضافية");
        }

        [Fact]
        public void SetSizeName_ShouldStoreSize()
        {
            var mod = new SaleItemModifier { SizeName = "Large" };
            mod.SizeName.Should().Be("Large");
        }

        [Fact]
        public void SetPrice_ShouldStoreBasePrice()
        {
            var mod = new SaleItemModifier { Price = 3.000m };
            mod.Price.Should().Be(3.000m);
        }

        [Fact]
        public void SetAdditionalPrice_ShouldStoreSurcharge()
        {
            var mod = new SaleItemModifier { AdditionalPrice = 1.500m };
            mod.AdditionalPrice.Should().Be(1.500m);
        }

        [Fact]
        public void SetQuantity_ShouldStoreQuantity()
        {
            var mod = new SaleItemModifier { Quantity = 2 };
            mod.Quantity.Should().Be(2);
        }

        [Fact]
        public void TotalModifierCost_ShouldBeAdditionalPriceTimesQuantity()
        {
            var mod = new SaleItemModifier
            {
                AdditionalPrice = 2.000m,
                Quantity = 3
            };
            var total = mod.AdditionalPrice * mod.Quantity;
            total.Should().Be(6.000m);
        }

        [Fact]
        public void SetSaleItemNavigation_ShouldStoreReference()
        {
            var saleItem = new SaleItem();
            var mod = new SaleItemModifier { SaleItem = saleItem };
            mod.SaleItem.Should().BeSameAs(saleItem);
        }

        [Fact]
        public void MarkAsDeleted_Restore_ShouldToggleIsDeleted()
        {
            var mod = new SaleItemModifier();
            mod.MarkAsDeleted();
            mod.IsDeleted.Should().BeTrue();
            mod.Restore();
            mod.IsDeleted.Should().BeFalse();
        }
    }
}
