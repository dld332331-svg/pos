using System.Text.Json;
using POS.Application.DTOs;
using POS.Application.Validators;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class SaleService : ISaleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IPromotionService _promotionService;
    private readonly IUnitConversionService _unitConversionService;

    public SaleService(IUnitOfWork unitOfWork, IAuditService auditService, IPromotionService? promotionService = null, IUnitConversionService? unitConversionService = null)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _promotionService = promotionService!;
        _unitConversionService = unitConversionService!;
    }

    public async Task<Guid> CreateNewSaleAsync(Guid userId, Guid shiftId, string? orderType = null, Guid? tableId = null)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var salesToday = (await _unitOfWork.Sales.FindAsync(s => s.InvoiceNumber.Contains($"INV-{today}"))).ToList();
        var nextSeq = salesToday.Count + 1;
        var invoiceNumber = $"INV-{today}-{nextSeq:D4}";

        var sale = new Sale
        {
            InvoiceNumber = invoiceNumber,
            ShiftId = shiftId,
            UserId = userId,
            OrderType = Enum.TryParse<OrderType>(orderType, ignoreCase: true, out var ot) ? ot : OrderType.Takeaway,
            TableId = tableId,
            Status = SaleStatus.Active,
            SubTotal = 0,
            TaxAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 0,
            IsPaid = false
        };

        await _unitOfWork.Sales.AddAsync(sale);
        await _unitOfWork.SaveChangesAsync();

        return sale.Id;
    }

    public async Task AddItemAsync(Guid saleId, AddItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
                ?? throw new InvalidOperationException("البيع غير موجود");

            if (sale.Status != SaleStatus.Active)
                throw new InvalidOperationException("لا يمكن إضافة عناصر لبيع غير نشط");

            var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId)
                ?? throw new InvalidOperationException("المنتج غير موجود");

            if (product.Status != ProductStatus.Active)
                throw new InvalidOperationException("المنتج غير نشط");

            // ── Unit Conversion & Display Tracking ──────────────────────────
            // If the request specifies a unit and the product has a UnitOfMeasure,
            // 1) convert the quantity to the product's default unit for inventory/pricing
            // 2) track the original (display) unit for UI display and held-sale retrieval
            decimal effectiveQuantity = request.Quantity;
            Guid? displayUnitId = null;
            decimal? displayQuantity = null;

            if (!string.IsNullOrWhiteSpace(request.Unit) && product.UnitOfMeasureId.HasValue && _unitConversionService is not null)
            {
                var units = await _unitConversionService.GetAllUnitsAsync();

                // Find the requested unit by symbol or arabic symbol
                var requestedUnit = units.FirstOrDefault(u =>
                    string.Equals(u.Symbol, request.Unit, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.ArabicSymbol, request.Unit, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.Name, request.Unit, StringComparison.OrdinalIgnoreCase));

                if (requestedUnit is not null && requestedUnit.Id != product.UnitOfMeasureId.Value)
                {
                    // Convert from the selected unit to the product's default unit
                    effectiveQuantity = await _unitConversionService.ConvertAsync(
                        request.Quantity, requestedUnit.Id, product.UnitOfMeasureId.Value);

                    // Track the display unit for UI and held-sale retrieval
                    displayUnitId = requestedUnit.Id;
                    displayQuantity = request.Quantity; // Pre-conversion value (e.g., 500 for g)
                }
            }

            // Check stock (in product's default unit)
            var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == product.Id)).FirstOrDefault();
            var availableQty = inventory?.AvailableQuantity ?? 0;
            if (availableQty < effectiveQuantity)
                throw new InvalidOperationException($"الكمية المتاحة غير كافية. المتاح: {availableQty}");

            // Calculate line amounts (using converted quantity)
            var baseLineAmount = MoneyPolicy.RoundToJOD(product.Price * effectiveQuantity);

            // Calculate modifier additional price
            decimal modifierExtra = 0;
            var modifierNames = new List<string>();

            if (request.Modifiers is not null && request.Modifiers.Count > 0)
            {
                foreach (var modSel in request.Modifiers)
                {
                    var modifier = await _unitOfWork.Modifiers.GetByIdAsync(modSel.ModifierId);
                    if (modifier is null) continue;

                    decimal modUnitPrice = modifier.Price;

                    // Check for size override
                    if (modSel.ModifierSizeId.HasValue)
                    {
                        var modSize = await _unitOfWork.ModifierSizes.GetByIdAsync(modSel.ModifierSizeId.Value);
                        if (modSize is not null)
                            modUnitPrice += modSize.PriceAdjustment;
                    }

                    // AdditionalPrice is per-unit, total is calculated as unitPrice * quantity
                    modifierExtra = MoneyPolicy.RoundToJOD(modifierExtra + modUnitPrice * modSel.Quantity);
                    modifierNames.Add(modifier.Name);
                }
            }

            var lineTotalBeforeTax = MoneyPolicy.RoundToJOD(baseLineAmount + modifierExtra);
            var taxAmount = MoneyPolicy.RoundToJOD(lineTotalBeforeTax * product.TaxRate);
            var lineTotal = MoneyPolicy.RoundToJOD(lineTotalBeforeTax + taxAmount);

            var saleItem = new SaleItem
            {
                SaleId = saleId,
                ProductId = product.Id,
                ProductName = product.ArabicName ?? string.Empty,
                Quantity = effectiveQuantity,
                UnitPrice = product.Price,
                Discount = 0,
                TaxRate = product.TaxRate,
                TaxAmount = taxAmount,
                LineTotal = lineTotal,
                Cost = product.Cost,
                Notes = request.Notes,
                ModifierSummary = modifierNames.Count > 0 ? string.Join(", ", modifierNames) : null,
                UnitOfMeasureId = displayUnitId,
                DisplayQuantity = displayQuantity
            };

            // Create modifier records
            if (request.Modifiers is not null)
            {
                foreach (var modSel in request.Modifiers)
                {
                    var modifier = await _unitOfWork.Modifiers.GetByIdAsync(modSel.ModifierId);
                    if (modifier is null) continue;

                    decimal modUnitPrice = modifier.Price;
                    if (modSel.ModifierSizeId.HasValue)
                    {
                        var modSize = await _unitOfWork.ModifierSizes.GetByIdAsync(modSel.ModifierSizeId.Value);
                        if (modSize is not null) modUnitPrice += modSize.PriceAdjustment;
                    }

                    saleItem.AddModifier(new SaleItemModifier
                    {
                        SaleItemId = saleItem.Id,
                        ModifierId = modSel.ModifierId,
                        ModifierName = modifier.Name,
                        AdditionalPrice = modUnitPrice,
                        Quantity = modSel.Quantity
                    });
                }
            }

            sale.AddItem(saleItem);

            // Reserve inventory (using converted quantity)
            if (inventory is not null)
            {
                inventory.ReservedQuantity += effectiveQuantity;
                await _unitOfWork.InventoryItems.UpdateAsync(inventory);
            }

            // Recalculate sale totals
            RecalculateSaleTotals(sale);

            await _unitOfWork.Sales.UpdateAsync(sale);
            await _unitOfWork.SaleItems.AddAsync(saleItem);

            // Auto-apply eligible promotions (best matching) before committing so
            // that all side effects remain inside the transaction.
            await TryAutoApplyPromotionsAsync(sale);

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task RemoveItemAsync(Guid saleId, Guid itemId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
                ?? throw new InvalidOperationException("البيع غير موجود");

            if (sale.Status != SaleStatus.Active)
                throw new InvalidOperationException("لا يمكن حذف عناصر من بيع غير نشط");

            var item = (await _unitOfWork.SaleItems.FindAsync(i => i.Id == itemId)).FirstOrDefault()
                ?? throw new InvalidOperationException("العنصر غير موجود");

            // Release reserved inventory
            var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == item.ProductId)).FirstOrDefault();
            if (inventory is not null)
            {
                inventory.ReservedQuantity = Math.Max(0, inventory.ReservedQuantity - item.Quantity);
                await _unitOfWork.InventoryItems.UpdateAsync(inventory);
            }

            sale.RemoveItem(item);
            RecalculateSaleTotals(sale);

            await _unitOfWork.SaleItems.DeleteAsync(item);
            await _unitOfWork.Sales.UpdateAsync(sale);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateItemQuantityAsync(Guid saleId, Guid itemId, decimal newQuantity)
    {
        if (newQuantity <= 0)
            throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
                ?? throw new InvalidOperationException("البيع غير موجود");

            if (sale.Status != SaleStatus.Active)
                throw new InvalidOperationException("لا يمكن تعديل بيع غير نشط");

            var item = (await _unitOfWork.SaleItems.FindAsync(i => i.Id == itemId)).FirstOrDefault()
                ?? throw new InvalidOperationException("العنصر غير موجود");

            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId)
                ?? throw new InvalidOperationException("المنتج غير موجود");

            var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == product.Id)).FirstOrDefault();
            var availableQty = inventory?.AvailableQuantity ?? 0;

            // Account for the quantity being released from the old reservation
            var qtyDiff = newQuantity - item.Quantity;
            if (qtyDiff > 0 && availableQty < qtyDiff)
                throw new InvalidOperationException($"الكمية المتاحة غير كافية. المتاح: {availableQty}");

            // Update reservation
            if (inventory is not null)
            {
                inventory.ReservedQuantity += qtyDiff;
                await _unitOfWork.InventoryItems.UpdateAsync(inventory);
            }

            // Recalculate line item
            item.Quantity = newQuantity;
            var baseLineAmount = MoneyPolicy.RoundToJOD(item.UnitPrice * newQuantity);

            // Recalculate modifier extras (AdditionalPrice is per-unit, so total = sum of unitPrice * quantity)
            decimal modifierExtra = 0;
            if (item.Modifiers.Count > 0)
            {
                modifierExtra = MoneyPolicy.RoundToJOD(item.Modifiers.Sum(m => m.AdditionalPrice * m.Quantity));
            }

            var lineTotalBeforeTax = MoneyPolicy.RoundToJOD(baseLineAmount + modifierExtra - item.Discount);
            item.TaxAmount = MoneyPolicy.RoundToJOD(lineTotalBeforeTax * item.TaxRate);
            item.LineTotal = MoneyPolicy.RoundToJOD(lineTotalBeforeTax + item.TaxAmount);
            item.MarkAsModified();

            RecalculateSaleTotals(sale);

            await _unitOfWork.SaleItems.UpdateAsync(item);
            await _unitOfWork.Sales.UpdateAsync(sale);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<SaleItemDto> ModifyItemAsync(Guid saleId, Guid itemId, ModifierSelectionDto[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
                ?? throw new InvalidOperationException("البيع غير موجود");

            if (sale.Status != SaleStatus.Active)
                throw new InvalidOperationException("لا يمكن تعديل بيع غير نشط");

            var item = (await _unitOfWork.SaleItems.FindAsync(i => i.Id == itemId)).FirstOrDefault()
                ?? throw new InvalidOperationException("العنصر غير موجود");

            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId)
                ?? throw new InvalidOperationException("المنتج غير موجود");

            // Remove old modifiers from DB
            var oldModifiers = (await _unitOfWork.SaleItemModifiers.FindAsync(m => m.SaleItemId == itemId)).ToList();
            foreach (var old in oldModifiers)
            {
                await _unitOfWork.SaleItemModifiers.DeleteAsync(old);
            }

            // Calculate new modifier extras
            decimal modifierExtra = 0;
            var modifierNames = new List<string>();

            foreach (var modSel in modifiers)
            {
                var modifier = await _unitOfWork.Modifiers.GetByIdAsync(modSel.ModifierId);
                if (modifier is null) continue;

                decimal modUnitPrice = modifier.Price;
                if (modSel.ModifierSizeId.HasValue)
                {
                    var modSize = await _unitOfWork.ModifierSizes.GetByIdAsync(modSel.ModifierSizeId.Value);
                    if (modSize is not null) modUnitPrice += modSize.PriceAdjustment;
                }

                // AdditionalPrice is per-unit, total modifier cost = unitPrice * quantity
                modifierExtra = MoneyPolicy.RoundToJOD(modifierExtra + modUnitPrice * modSel.Quantity);
                modifierNames.Add(modifier.Name);

                var sim = new SaleItemModifier
                {
                    SaleItemId = itemId,
                    ModifierId = modSel.ModifierId,
                    ModifierName = modifier.Name,
                    AdditionalPrice = modUnitPrice,
                    Quantity = modSel.Quantity
                };
                await _unitOfWork.SaleItemModifiers.AddAsync(sim);
                item.AddModifier(sim);
            }

            // Recalculate line totals
            var baseLineAmount = MoneyPolicy.RoundToJOD(item.UnitPrice * item.Quantity);
            var lineTotalBeforeTax = MoneyPolicy.RoundToJOD(baseLineAmount + modifierExtra - item.Discount);
            item.TaxAmount = MoneyPolicy.RoundToJOD(lineTotalBeforeTax * item.TaxRate);
            item.LineTotal = MoneyPolicy.RoundToJOD(lineTotalBeforeTax + item.TaxAmount);
            item.ModifierSummary = modifierNames.Count > 0 ? string.Join(", ", modifierNames) : null;
            item.MarkAsModified();

            RecalculateSaleTotals(sale);

            await _unitOfWork.Sales.UpdateAsync(sale);
            await _unitOfWork.CommitAsync();

            return MapSaleItemToDto(item);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task ApplyDiscountAsync(ApplyDiscountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sale = await _unitOfWork.Sales.GetByIdAsync(request.SaleId)
                ?? throw new InvalidOperationException("البيع غير موجود");

            if (sale.Status != SaleStatus.Active)
                throw new InvalidOperationException("لا يمكن تطبيق خصم على بيع غير نشط");

            var errors = SaleValidator.ValidateDiscount(request.DiscountAmount, sale.SubTotal);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(", ", errors));

            sale.DiscountAmount = MoneyPolicy.RoundToJOD(request.DiscountAmount);

            // Distribute the invoice-level discount proportionally across all line items
            // for record-keeping in SaleItem.DiscountAmount (reporting field)
            // The actual TotalAmount calculation remains: SubTotal + TaxAmount - DiscountAmount
            if (sale.SubTotal > 0 && request.DiscountAmount > 0)
            {
                var items = sale.SaleItems;
                var discountRatio = MoneyPolicy.RoundToJOD(request.DiscountAmount / sale.SubTotal);
                decimal distributedDiscount = 0;

                foreach (var item in items)
                {
                    var itemBeforeModifiers = MoneyPolicy.RoundToJOD(item.UnitPrice * item.Quantity);
                    var itemDiscount = MoneyPolicy.RoundToJOD(itemBeforeModifiers * discountRatio);
                    item.DiscountAmount = MoneyPolicy.RoundToJOD(item.DiscountAmount + itemDiscount);
                    distributedDiscount = MoneyPolicy.RoundToJOD(distributedDiscount + itemDiscount);
                }

                // Adjust rounding difference to ensure total matches
                var roundingDiff = MoneyPolicy.RoundToJOD(request.DiscountAmount - distributedDiscount);
                if (roundingDiff != 0 && items.Count > 0)
                {
                    items.Last().DiscountAmount = MoneyPolicy.RoundToJOD(items.Last().DiscountAmount + roundingDiff);
                }
            }

            RecalculateSaleTotals(sale);

            await _unitOfWork.Sales.UpdateAsync(sale);
            await _auditService.LogAsync(sale.UserId, AuditActionType.DiscountApplied, "Sale", sale.Id,
                null, $"Discount={request.DiscountAmount}", request.Reason);

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sale = await _unitOfWork.Sales.GetByIdAsync(request.SaleId)
            ?? throw new InvalidOperationException("البيع غير موجود");

        if (sale.Status != SaleStatus.Active)
            return new PaymentResult(false, 0, "البيع غير نشط");

        // Skip payment validation for credit sales
        var isCredit = string.Equals(request.PaymentMethod, "آجل", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase);
        if (!isCredit)
        {
            var errors = SaleValidator.ValidatePayment(sale.TotalAmount, request.Amount);
            if (errors.Count > 0)
                return new PaymentResult(false, 0, string.Join(", ", errors));
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Map UI method strings to PaymentMethod enum
            var methodMap = new Dictionary<string, PaymentMethod>(StringComparer.OrdinalIgnoreCase)
            {
                ["نقداً"] = PaymentMethod.Cash,
                ["بطاقة"] = PaymentMethod.Card,
                ["محفظة إلكترونية"] = PaymentMethod.EWallet,
                ["آجل"] = PaymentMethod.Credit,
            };

            var paymentMethod = methodMap.TryGetValue(request.PaymentMethod, out var mapped)
                ? mapped
                : Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out var pm)
                    ? pm
                    : PaymentMethod.Cash;

            // Set customer for credit sales
            if (request.CustomerId.HasValue)
            {
                sale.CustomerId = request.CustomerId;
            }

            // Create payment record (skip for credit with zero amount)
            Payment? payment = null;
            if (request.Amount > 0 || paymentMethod != PaymentMethod.Credit)
            {
                payment = new Payment
                {
                    SaleId = request.SaleId,
                    PaymentMethod = paymentMethod,
                    Amount = MoneyPolicy.RoundToJOD(request.Amount),
                    ReferenceNumber = request.ReferenceNumber,
                    Timestamp = DateTime.UtcNow
                };

                sale.AddPayment(payment);
                await _unitOfWork.Payments.AddAsync(payment);
            }

            // Update sale status
            sale.IsPaid = true;
            sale.PaidAt = DateTime.UtcNow;
            sale.Status = SaleStatus.Completed;

            // Deduct inventory for each item (FIFO batch-aware)
            var items = (await _unitOfWork.SaleItems.FindAsync(i => i.SaleId == sale.Id)).ToList();
            foreach (var item in items)
            {
                var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == item.ProductId)).FirstOrDefault();
                if (inventory is null) continue;

                var beforeQty = inventory.Quantity;
                var remainingToDeduct = item.Quantity;

                // Try batch-level FIFO deduction
                var batches = (await _unitOfWork.InventoryBatches.FindAsync(b => b.InventoryItemId == inventory.Id && b.Quantity > 0))
                    .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.ReceivedDate)
                    .ToList();

                if (batches.Count != 0)
                {
                    foreach (var batch in batches)
                    {
                        if (remainingToDeduct <= 0) break;

                        var deductFromBatch = Math.Min(batch.Quantity, remainingToDeduct);
                        var batchBeforeQty = batch.Quantity;
                        batch.Quantity = MoneyPolicy.RoundToJOD(batch.Quantity - deductFromBatch);
                        remainingToDeduct = MoneyPolicy.RoundToJOD(remainingToDeduct - deductFromBatch);

                        await _unitOfWork.InventoryBatches.UpdateAsync(batch);

                        var batchMovement = new InventoryMovement
                        {
                            ProductId = item.ProductId,
                            InventoryItemId = inventory.Id,
                            MovementType = MovementType.Sale,
                            Quantity = -deductFromBatch,
                            BeforeQuantity = batchBeforeQty,
                            AfterQuantity = batch.Quantity,
                            Reason = $"Sale {sale.InvoiceNumber} (batch: {batch.BatchNumber})",
                            UserId = sale.UserId,
                            InventoryBatchId = batch.Id,
                            Reference = sale.Id.ToString()
                        };
                        await _unitOfWork.InventoryMovements.AddAsync(batchMovement);
                    }
                }

                // Update aggregate inventory quantity
                inventory.Quantity = Math.Max(0, inventory.Quantity - item.Quantity);
                inventory.ReservedQuantity = Math.Max(0, inventory.ReservedQuantity - item.Quantity);
                await _unitOfWork.InventoryItems.UpdateAsync(inventory);

                // Create aggregate movement if no batches were used
                if (batches.Count == 0)
                {
                    var movement = new InventoryMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = MovementType.Sale,
                        Quantity = -item.Quantity,
                        BeforeQuantity = beforeQty,
                        AfterQuantity = inventory.Quantity,
                        Reason = $"Sale {sale.InvoiceNumber}",
                        UserId = sale.UserId,
                        Reference = sale.Id.ToString()
                    };
                    await _unitOfWork.InventoryMovements.AddAsync(movement);
                }
            }

            // Update shift totals
            var shift = await _unitOfWork.Shifts.GetByIdAsync(sale.ShiftId);
            if (shift is not null && shift.Status == ShiftStatus.Open)
            {
                shift.TotalSales += sale.TotalAmount;
                await _unitOfWork.Shifts.UpdateAsync(shift);
            }

            // Release held sale if this was a retrieved one
            var heldSales = (await _unitOfWork.HeldSales.FindAsync(h => h.SerializedData.Contains(sale.Id.ToString()))).ToList();

            await _unitOfWork.Sales.UpdateAsync(sale);

            var changeAmount = MoneyPolicy.RoundToJOD(request.Amount - sale.TotalAmount);

            await _auditService.LogAsync(sale.UserId, AuditActionType.SaleCompleted, "Sale", sale.Id,
                null, $"Total={sale.TotalAmount},Payment={request.Amount}", null);
            if (payment != null)
            {
                await _auditService.LogAsync(sale.UserId, AuditActionType.PaymentProcessed, "Payment", payment.Id,
                    null, $"Method={paymentMethod},Amount={payment.Amount}", null);
            }

            await _unitOfWork.CommitAsync();

            return new PaymentResult(true, changeAmount);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<SaleSummaryDto> GetSaleSummaryAsync(Guid saleId)
    {
        var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
            ?? throw new InvalidOperationException("البيع غير موجود");

        return new SaleSummaryDto(
            sale.Id,
            sale.InvoiceNumber,
            sale.SubTotal,
            sale.TaxAmount,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.Status.ToString(),
            sale.CreatedAt);
    }

    public async Task<List<SaleItemDto>> GetSaleItemsAsync(Guid saleId)
    {
        var items = await _unitOfWork.SaleItems.FindAsync(i => i.SaleId == saleId);
        return items.Select(MapSaleItemToDto).ToList();
    }

    public async Task<Guid> HoldSaleAsync(Guid saleId, string reason)
    {
        var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
            ?? throw new InvalidOperationException("البيع غير موجود");

        if (sale.Status != SaleStatus.Active)
            throw new InvalidOperationException("يمكن فقط وضع البيع النشط في الانتظار");

        sale.Status = SaleStatus.Held;
        await _unitOfWork.Sales.UpdateAsync(sale);

        var items = await _unitOfWork.SaleItems.FindAsync(i => i.SaleId == saleId);
        var serializedItems = items.Select(i => new
        {
            i.ProductId,
            i.ProductName,
            i.Quantity,
            i.UnitPrice,
            i.Discount,
            i.TaxRate,
            i.TaxAmount,
            i.LineTotal,
            i.Cost,
            i.Notes,
            i.ModifierSummary,
            i.UnitOfMeasureId,
            i.DisplayQuantity
        }).ToList();

        var heldSale = new HeldSale
        {
            SerializedData = JsonSerializer.Serialize(new
            {
                SaleId = sale.Id,
                InvoiceNumber = sale.InvoiceNumber,
                Items = serializedItems,
                SubTotal = sale.SubTotal,
                TaxAmount = sale.TaxAmount,
                DiscountAmount = sale.DiscountAmount,
                TotalAmount = sale.TotalAmount
            }),
            ShiftId = sale.ShiftId,
            UserId = sale.UserId,
            HoldReason = reason
        };

        await _unitOfWork.HeldSales.AddAsync(heldSale);
        await _unitOfWork.SaveChangesAsync();

        return heldSale.Id;
    }

    public async Task<SaleSummaryDto> RetrieveHeldSaleAsync(Guid heldSaleId)
    {
        var heldSale = await _unitOfWork.HeldSales.GetByIdAsync(heldSaleId)
            ?? throw new InvalidOperationException("البيع المحتفظ به غير موجود");

        var saleId = Guid.Empty;
        decimal totalAmount = 0;

        try
        {
            var data = JsonDocument.Parse(heldSale.SerializedData);
            if (data.RootElement.TryGetProperty("SaleId", out var saleIdEl))
                saleId = saleIdEl.GetGuid();
            if (data.RootElement.TryGetProperty("TotalAmount", out var totalEl))
                totalAmount = totalEl.GetDecimal();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[SaleService] Failed to parse HeldSale serialized data for {heldSale.Id}: {ex.Message}");
        }

        var sale = await _unitOfWork.Sales.GetByIdAsync(saleId);
        if (sale is not null)
        {
            sale.Status = SaleStatus.Active;
            await _unitOfWork.Sales.UpdateAsync(sale);
        }

        // Remove held record
        await _unitOfWork.HeldSales.DeleteAsync(heldSale);
        await _unitOfWork.SaveChangesAsync();

        return new SaleSummaryDto(
            saleId,
            $"Held-{heldSale.HoldReason}",
            0, 0, 0, totalAmount,
            "Active",
            heldSale.CreatedAt);
    }

    public async Task<List<HeldSaleDto>> GetHeldSalesAsync(Guid shiftId)
    {
        var heldSales = await _unitOfWork.HeldSales.FindAsync(h => h.ShiftId == shiftId);
        var result = new List<HeldSaleDto>();

        foreach (var hs in heldSales)
        {
            decimal totalAmount = 0;
            try
            {
                var data = JsonDocument.Parse(hs.SerializedData);
                if (data.RootElement.TryGetProperty("TotalAmount", out var totalEl))
                    totalAmount = totalEl.GetDecimal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"[SaleService] Failed to parse HeldSale serialized data for {hs.Id}: {ex.Message}");
            }

            result.Add(new HeldSaleDto(hs.Id, hs.CreatedAt, hs.HoldReason ?? string.Empty, totalAmount));
        }

        return result;
    }

    public async Task<OperationResult> CancelSaleAsync(Guid saleId, string reason)
    {
        var sale = await _unitOfWork.Sales.GetByIdAsync(saleId);
        if (sale is null)
            return new OperationResult(false, ErrorMessage: "البيع غير موجود");

        if (sale.Status is SaleStatus.Completed or SaleStatus.Cancelled)
            return new OperationResult(false, ErrorMessage: "لا يمكن إلغاء هذا البيع");


        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Return reserved inventory
            var items = (await _unitOfWork.SaleItems.FindAsync(i => i.SaleId == saleId)).ToList();
            foreach (var item in items)
            {
                var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == item.ProductId)).FirstOrDefault();
                if (inventory is not null)
                {
                    inventory.ReservedQuantity = Math.Max(0, inventory.ReservedQuantity - item.Quantity);
                    await _unitOfWork.InventoryItems.UpdateAsync(inventory);
                }
            }

            sale.Status = SaleStatus.Cancelled;
            await _unitOfWork.Sales.UpdateAsync(sale);

            await _auditService.LogAsync(sale.UserId, AuditActionType.CancellationProcessed, "Sale", sale.Id,
                $"Status={SaleStatus.Active}", $"Status={SaleStatus.Cancelled}", reason);

            await _unitOfWork.CommitAsync();

            return new OperationResult(true, SuccessMessage: "تم إلغاء البيع بنجاح");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<OperationResult> ReturnItemsAsync(Guid originalSaleId, List<ReturnItemRequest> items, string reason)
    {
        ArgumentNullException.ThrowIfNull(items);
        var originalSale = await _unitOfWork.Sales.GetByIdAsync(originalSaleId);
        if (originalSale is null)
            return new OperationResult(false, ErrorMessage: "البيع الأصلي غير موجود");

        if (originalSale.Status != SaleStatus.Completed)
            return new OperationResult(false, ErrorMessage: "يمكن فقط إرجاع عناصر من بيع مكتمل");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            decimal totalReturnAmount = 0;

            var returnEntity = new Return
            {
                OriginalSaleId = originalSaleId,
                UserId = originalSale.UserId,
                TotalAmount = 0,
                Reason = reason,
                Status = "Processed"
            };

            foreach (var returnItem in items)
            {
                var saleItem = (await _unitOfWork.SaleItems.FindAsync(i => i.Id == returnItem.SaleItemId)).FirstOrDefault()
                    ?? throw new InvalidOperationException($"عنصر البيع {returnItem.SaleItemId} غير موجود");

                if (returnItem.Quantity > saleItem.Quantity)
                    throw new InvalidOperationException("الكمية المرتجعة أكبر من الكمية المباعة");

                var itemReturnAmount = MoneyPolicy.RoundToJOD(saleItem.UnitPrice * returnItem.Quantity);
                totalReturnAmount = MoneyPolicy.RoundToJOD(totalReturnAmount + itemReturnAmount);

                var returnItemEntity = new ReturnItem
                {
                    ReturnId = returnEntity.Id,
                    SaleItemId = returnItem.SaleItemId,
                    ProductId = saleItem.ProductId,
                    ProductName = saleItem.ProductName,
                    Quantity = returnItem.Quantity,
                    UnitPrice = saleItem.UnitPrice,
                    ReturnAmount = itemReturnAmount,
                    Reason = returnItem.Reason
                };

                returnEntity.AddItem(returnItemEntity);

                // Restore inventory (batch-aware)
                var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == saleItem.ProductId)).FirstOrDefault();
                if (inventory is not null)
                {
                    var beforeQty = inventory.Quantity;
                    inventory.Quantity += returnItem.Quantity;
                    var afterQty = inventory.Quantity;

                    await _unitOfWork.InventoryItems.UpdateAsync(inventory);

                    var movement = new InventoryMovement
                    {
                        ProductId = saleItem.ProductId,
                        MovementType = MovementType.Return,
                        Quantity = returnItem.Quantity,
                        BeforeQuantity = beforeQty,
                        AfterQuantity = afterQty,
                        Reason = $"Return from {originalSale.InvoiceNumber}",
                        UserId = originalSale.UserId,
                        Reference = returnEntity.Id.ToString()
                    };
                    await _unitOfWork.InventoryMovements.AddAsync(movement);

                    // Restore to latest active batch, or create a return batch
                    var latestBatch = (await _unitOfWork.InventoryBatches.FindAsync(b => b.InventoryItemId == inventory.Id))
                        .OrderByDescending(b => b.ReceivedDate)
                        .FirstOrDefault();
                    if (latestBatch is not null)
                    {
                        latestBatch.Quantity = MoneyPolicy.RoundToJOD(latestBatch.Quantity + returnItem.Quantity);
                        await _unitOfWork.InventoryBatches.UpdateAsync(latestBatch);
                        movement.InventoryBatchId = latestBatch.Id;
                    }
                    else
                    {
                        var returnBatch = new InventoryBatch
                        {
                            InventoryItemId = inventory.Id,
                            BatchNumber = $"RET-{originalSale.InvoiceNumber}-{DateTime.UtcNow:yyyyMMdd}",
                            Quantity = returnItem.Quantity,
                            UnitCost = saleItem.UnitPrice,
                            ReceivedDate = DateTime.UtcNow
                        };
                        await _unitOfWork.InventoryBatches.AddAsync(returnBatch);
                        movement.InventoryBatchId = returnBatch.Id;
                    }
                }
            }

            returnEntity.TotalAmount = totalReturnAmount;

            // Update shift returns total
            var shift = await _unitOfWork.Shifts.GetByIdAsync(originalSale.ShiftId);
            if (shift is not null && shift.Status == ShiftStatus.Open)
            {
                shift.TotalReturns += totalReturnAmount;
                await _unitOfWork.Shifts.UpdateAsync(shift);
            }

            originalSale.Status = SaleStatus.Returned;
            await _unitOfWork.Sales.UpdateAsync(originalSale);

            await _unitOfWork.Returns.AddAsync(returnEntity);

            await _auditService.LogAsync(originalSale.UserId, AuditActionType.ReturnProcessed, "Return", returnEntity.Id,
                null, $"Amount={totalReturnAmount}", reason);

            await _unitOfWork.CommitAsync();

            return new OperationResult(true, SuccessMessage: $"تم إرجاع المبلغ {totalReturnAmount} بنجاح");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<List<SaleSummaryDto>> GetSalesHistoryAsync(DateTime? from, DateTime? to, int page = 1, int pageSize = 20)
    {
        var allSales = await _unitOfWork.Sales.GetAllAsync();
        var filtered = allSales.AsQueryable();

        if (from.HasValue)
            filtered = filtered.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue)
            filtered = filtered.Where(s => s.CreatedAt <= to.Value.AddDays(1));

        return filtered
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SaleSummaryDto(
                s.Id,
                s.InvoiceNumber,
                s.SubTotal,
                s.TaxAmount,
                s.DiscountAmount,
                s.TotalAmount,
                s.Status.ToString(),
                s.CreatedAt))
            .ToList();
    }

    private static void RecalculateSaleTotals(Sale sale)
    {
        var items = sale.SaleItems;

        decimal subTotal = 0;
        decimal taxAmount = 0;

        foreach (var item in items)
        {
            // Include modifier amounts in the line total calculation
            decimal modifierExtra = 0;
            if (item.Modifiers.Count > 0)
            {
                modifierExtra = MoneyPolicy.RoundToJOD(item.Modifiers.Sum(m => m.AdditionalPrice * m.Quantity));
            }

            var lineBeforeTax = MoneyPolicy.RoundToJOD(
                item.UnitPrice * item.Quantity + modifierExtra - item.Discount);
            var itemTax = MoneyPolicy.RoundToJOD(lineBeforeTax * item.TaxRate);
            var lineTotal = MoneyPolicy.RoundToJOD(lineBeforeTax + itemTax);

            item.TaxAmount = itemTax;
            item.LineTotal = lineTotal;

            subTotal += lineBeforeTax;
            taxAmount += itemTax;
        }

        sale.SubTotal = MoneyPolicy.RoundToJOD(subTotal);
        sale.TaxAmount = MoneyPolicy.RoundToJOD(taxAmount);
        sale.TotalAmount = MoneyPolicy.RoundToJOD(sale.SubTotal + sale.TaxAmount - sale.DiscountAmount);
    }

    private static SaleItemDto MapSaleItemToDto(SaleItem item)
    {
        // Determine the display unit name from the UnitOfMeasure navigation property
        // or use the fallback from the product's Unit string.
        string? unitName = null;
        if (item.UnitOfMeasureId.HasValue)
        {
            unitName = item.UnitOfMeasure?.Symbol
                ?? item.UnitOfMeasure?.ArabicSymbol
                ?? item.UnitOfMeasure?.Name;
        }

        return new SaleItemDto(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.Quantity,
            item.UnitPrice,
            item.Discount,
            item.TaxRate,
            item.TaxAmount,
            item.LineTotal,
            item.Cost,
            item.Notes,
            item.ModifierSummary,
            Unit: unitName,
            UnitOfMeasureId: item.UnitOfMeasureId,
            UnitName: unitName);
    }

    public async Task<SaleSummaryDto?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return null;

        var sales = await _unitOfWork.Sales.FindAsync(s =>
            s.InvoiceNumber != null && s.InvoiceNumber.Contains(invoiceNumber));
        var sale = sales.FirstOrDefault();
        if (sale is null)
            return null;

        return new SaleSummaryDto(
            sale.Id,
            sale.InvoiceNumber ?? string.Empty,
            sale.SubTotal,
            sale.TaxAmount,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.Status.ToString(),
            sale.CreatedAt);
    }

    private async Task TryAutoApplyPromotionsAsync(Sale sale)
    {
        if (_promotionService == null) return;

        var items = sale.SaleItems.Select(MapSaleItemToDto).ToList();
        var eligible = await _promotionService.GetEligiblePromotionsAsync(sale.Id, items);
        if (eligible.Count == 0) return;

        var best = eligible.OrderByDescending(p => p.DiscountAmount).First();
        await _promotionService.ApplyPromotionAsync(sale.Id, best.PromotionId, items);
    }

    public async Task<List<AppliedPromotionDto>> GetAppliedPromotionsAsync(Guid saleId)
    {
        var salePromotions = await _unitOfWork.SalePromotions.FindAsync(sp => sp.SaleId == saleId);
        return salePromotions
            .OrderBy(sp => sp.CreatedAt)
            .Select(sp => new AppliedPromotionDto(
                sp.PromotionId, sp.Description ?? "", sp.DiscountAmount, sp.Description))
            .ToList();
    }
}