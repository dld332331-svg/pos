using POS.Application.DTOs;
using POS.Domain.BusinessRules;

namespace POS.Application.Services;

/// <summary>
/// Pure computation helper that keeps sale-related business logic out of UI forms.
/// All methods are stateless and work with DTOs, not entities.
/// </summary>
public static class SaleCalculator
{
    /// <summary>
    /// Recalculates TaxAmount and LineTotal for a single sale item DTO.
    /// Formula: LineTotal = (UnitPrice * Quantity + modifierExtra) * (1 + TaxRate/100)
    /// </summary>
    public static SaleItemDto RecalculateItem(SaleItemDto item, decimal modifierExtra = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lineBeforeTax = MoneyPolicy.RoundToJOD(
            item.UnitPrice * item.Quantity + modifierExtra - item.Discount);
        var taxAmount = MoneyPolicy.RoundToJOD(lineBeforeTax * item.TaxRate);
        var lineTotal = MoneyPolicy.RoundToJOD(lineBeforeTax + taxAmount);

        return item with { TaxAmount = taxAmount, LineTotal = lineTotal };
    }

    /// <summary>
    /// Creates a new SaleItemDto with computed TaxAmount and LineTotal from a ProductDto.
    /// </summary>
    public static SaleItemDto CreateItem(Guid? id, ProductDto product, decimal quantity, decimal discount = 0)
    {
        ArgumentNullException.ThrowIfNull(product);
        var lineBeforeTax = MoneyPolicy.RoundToJOD(product.SellingPrice * quantity + 0 - discount);
        var taxAmount = MoneyPolicy.RoundToJOD(lineBeforeTax * product.TaxRate);
        var lineTotal = MoneyPolicy.RoundToJOD(lineBeforeTax + taxAmount);

        return new SaleItemDto(
            id ?? Guid.NewGuid(),
            product.Id,
            product.ArabicName ?? string.Empty,
            quantity,
            product.SellingPrice,
            discount,
            product.TaxRate,
            taxAmount,
            lineTotal,
            product.Cost,
            null,
            null);
    }

    /// <summary>
    /// Creates a new SaleItemDto with computed values from explicit parameters.
    /// Used when incrementing quantity of an existing item.
    /// </summary>
    public static SaleItemDto CreateItem(
        Guid? id, Guid productId, string productName, decimal unitPrice,
        decimal quantity, decimal discount, decimal taxRate, decimal cost,
        string? notes, string? modifierSummary)
    {
        var lineBeforeTax = MoneyPolicy.RoundToJOD(
            unitPrice * quantity + 0 - discount);
        var taxAmount = MoneyPolicy.RoundToJOD(lineBeforeTax * taxRate);
        var lineTotal = MoneyPolicy.RoundToJOD(lineBeforeTax + taxAmount);

        return new SaleItemDto(
            id ?? Guid.NewGuid(),
            productId,
            productName,
            quantity,
            unitPrice,
            discount,
            taxRate,
            taxAmount,
            lineTotal,
            cost,
            notes,
            modifierSummary);
    }

    /// <summary>
    /// Computes the overall sale totals from a list of sale item DTOs.
    /// </summary>
    public static SaleTotalsDto CalculateTotals(IEnumerable<SaleItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var itemList = items.ToList();
        var subtotal = itemList.Sum(i => i.UnitPrice * i.Quantity);
        var tax = itemList.Sum(i => i.TaxAmount);
        var discount = itemList.Sum(i => i.Discount);
        var total = subtotal + tax - discount;

        return new SaleTotalsDto(
            MoneyPolicy.RoundToJOD(subtotal),
            MoneyPolicy.RoundToJOD(tax),
            MoneyPolicy.RoundToJOD(discount),
            MoneyPolicy.RoundToJOD(total));
    }

    /// <summary>
    /// Returns the simple total used for payment: Sum(LineTotal) - Sum(Discount).
    /// </summary>
    public static decimal GetTotal(IEnumerable<SaleItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var itemList = items.ToList();
        return MoneyPolicy.RoundToJOD(
            itemList.Sum(i => i.LineTotal) - itemList.Sum(i => i.Discount));
    }

    /// <summary>
    /// Returns the change amount: amountPaid - totalDue.
    /// </summary>
    public static decimal GetChange(decimal amountPaid, decimal totalDue)
    {
        return MoneyPolicy.RoundToJOD(amountPaid - totalDue);
    }

    /// <summary>
    /// Distributes a discount amount proportionally across all sale items.
    /// Returns a new list with updated Discount values on each item.
    /// </summary>
    public static List<SaleItemDto> DistributeDiscount(List<SaleItemDto> items, decimal discountAmount)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || discountAmount <= 0)
            return items.ToList();

        var total = GetTotal(items);
        if (total <= 0)
            return items.ToList();

        var result = new List<SaleItemDto>();
        decimal distributedDiscount = 0;

        foreach (var item in items)
        {
            var proportion = item.LineTotal / total;
            var itemDiscount = MoneyPolicy.RoundToJOD(discountAmount * proportion);
            result.Add(item with { Discount = MoneyPolicy.RoundToJOD(item.Discount + itemDiscount) });
            distributedDiscount = MoneyPolicy.RoundToJOD(distributedDiscount + itemDiscount);
        }

        // Adjust rounding difference to ensure total matches
        var roundingDiff = MoneyPolicy.RoundToJOD(discountAmount - distributedDiscount);
        if (roundingDiff != 0 && result.Count > 0)
        {
            var last = result[^1];
            result[^1] = last with { Discount = MoneyPolicy.RoundToJOD(last.Discount + roundingDiff) };
        }

        return result;
    }
}

/// <summary>
/// DTO that holds computed sale totals.
/// </summary>
public record SaleTotalsDto(decimal SubTotal, decimal Tax, decimal Discount, decimal Total);
