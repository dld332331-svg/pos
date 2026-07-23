using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public InventoryService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<List<InventoryStatusDto>> GetCurrentStockAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventory.ToDictionary(i => i.ProductId);

        var result = new List<InventoryStatusDto>();

        foreach (var product in products.Where(p => p.Status == ProductStatus.Active))
        {
            inventoryMap.TryGetValue(product.Id, out var inv);
            var qty = inv?.Quantity ?? 0;
            var reserved = inv?.ReservedQuantity ?? 0;
            var available = qty - reserved;

            result.Add(new InventoryStatusDto(
                product.Id,
                product.ArabicName ?? "Unknown",
                qty,
                reserved,
                available,
                product.Unit,
                product.MinStock,
                available <= product.MinStock));
        }

        return result;
    }

    public async Task<List<InventoryStatusDto>> GetLowStockAsync()
    {
        var stock = await GetCurrentStockAsync();
        return stock.Where(s => s.IsLowStock).ToList();
    }

    public async Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(
        Guid? productId, DateTime? from, DateTime? to, int page = 1, int pageSize = 20)
    {
        var movements = (await _unitOfWork.InventoryMovements.GetAllAsync()).AsQueryable();
        var products = await _unitOfWork.Products.GetAllAsync();
        var users = await _unitOfWork.Users.GetAllAsync();
        var productMap = products.ToDictionary(p => p.Id, p => p.ArabicName);
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);

        if (productId.HasValue)
            movements = movements.Where(m => m.ProductId == productId.Value);

        if (from.HasValue)
            movements = movements.Where(m => m.Timestamp >= from.Value);
        if (to.HasValue)
            movements = movements.Where(m => m.Timestamp <= to.Value.AddDays(1));

        var total = movements.Count();
        var items = movements
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.ProductId,
                productMap.GetValueOrDefault(m.ProductId, "Unknown") ?? "Unknown",
                m.MovementType.ToString(),
                m.Quantity,
                m.BeforeQuantity,
                m.AfterQuantity,
                m.Reason,
                userMap.GetValueOrDefault(m.UserId, "System"),
                m.Timestamp,
                m.Reference))
            .ToList();

        return new PagedResult<InventoryMovementDto>(items, total, page, pageSize);
    }

    public async Task<OperationResult> AdjustStockAsync(StockAdjustmentRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
        if (product is null)
            return new OperationResult(false, ErrorMessage: "المنتج غير موجود");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == request.ProductId)).FirstOrDefault();

            if (inventory is null)
            {
                // Create inventory record if it doesn't exist
                inventory = new InventoryItem
                {
                    ProductId = request.ProductId,
                    Quantity = 0,
                    ReservedQuantity = 0
                };
                await _unitOfWork.InventoryItems.AddAsync(inventory);
            }

            if (request.NewQuantity < 0)
                return new OperationResult(false, ErrorMessage: "الكمية لا يمكن أن تكون سالبة");

            var beforeQty = inventory.Quantity;
            var difference = request.NewQuantity - inventory.Quantity;
            inventory.Quantity = request.NewQuantity;
            inventory.MarkAsModified(userId);

            var movement = new InventoryMovement
            {
                ProductId = request.ProductId,
                MovementType = MovementType.Adjustment,
                Quantity = difference,
                BeforeQuantity = beforeQty,
                AfterQuantity = request.NewQuantity,
                Reason = request.Reason,
                UserId = userId
            };

            await _unitOfWork.InventoryItems.UpdateAsync(inventory);
            await _unitOfWork.InventoryMovements.AddAsync(movement);

            await _auditService.LogAsync(userId, AuditActionType.InventoryAdjusted, "InventoryItem", inventory.Id,
                $"Quantity={beforeQty}", $"Quantity={request.NewQuantity}", request.Reason);

            await _unitOfWork.CommitAsync();

            return new OperationResult(true, SuccessMessage: "تم تعديل المخزون بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            System.Diagnostics.Trace.TraceError("AdjustStockAsync failed: {0}", ex);
            return new OperationResult(false, ErrorMessage: "حدث خطأ أثناء تعديل المخزون");
        }
    }

    public async Task<OperationResult> RecordWasteAsync(WasteRecordRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
        if (product is null)
            return new OperationResult(false, ErrorMessage: "المنتج غير موجود");

        if (request.Quantity <= 0)
            return new OperationResult(false, ErrorMessage: "الكمية يجب أن تكون أكبر من صفر");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var inventory = (await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == request.ProductId)).FirstOrDefault();

            if (inventory is null)
            {
                inventory = new InventoryItem
                {
                    ProductId = request.ProductId,
                    Quantity = 0,
                    ReservedQuantity = 0
                };
                await _unitOfWork.InventoryItems.AddAsync(inventory);
            }

            if (inventory.Quantity < request.Quantity)
                return new OperationResult(false, ErrorMessage: "الكمية المطلوبة إتلافها أكبر من المخزون المتاح");

            var beforeQty = inventory.Quantity;
            inventory.Quantity = MoneyPolicy.RoundToJOD(inventory.Quantity - request.Quantity);
            inventory.MarkAsModified(userId);

            var movement = new InventoryMovement
            {
                ProductId = request.ProductId,
                MovementType = MovementType.Waste,
                Quantity = -request.Quantity,
                BeforeQuantity = beforeQty,
                AfterQuantity = inventory.Quantity,
                Reason = request.Reason,
                UserId = userId
            };

            await _unitOfWork.InventoryItems.UpdateAsync(inventory);
            await _unitOfWork.InventoryMovements.AddAsync(movement);

            await _auditService.LogAsync(userId, AuditActionType.WasteRecorded, "InventoryItem", inventory.Id,
                $"Quantity={beforeQty}", $"Quantity={inventory.Quantity}", request.Reason);

            await _unitOfWork.CommitAsync();

            return new OperationResult(true, SuccessMessage: "تم تسجيل الإتلاف بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            System.Diagnostics.Trace.TraceError("RecordWasteAsync failed: {0}", ex);
            return new OperationResult(false, ErrorMessage: "حدث خطأ أثناء تسجيل الإتلاف");
        }
    }

    public async Task<OperationResult> ProcessPurchaseReceivedAsync(Guid purchaseOrderId, Guid userId)
    {
        var po = await _unitOfWork.PurchaseOrders.GetByIdAsync(purchaseOrderId);
        if (po is null)
            return new OperationResult(false, ErrorMessage: "أمر الشراء غير موجود");

        if (po.Status != "Pending")
            return new OperationResult(false, ErrorMessage: "أمر الشراء ليس في حالة انتظار");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var poItems = (await _unitOfWork.PurchaseOrderItems.FindAsync(i => i.PurchaseOrderId == purchaseOrderId)).ToList();

            foreach (var poItem in poItems)
            {
                var qtyToAdd = MoneyPolicy.RoundToJOD(poItem.Quantity - poItem.ReceivedQuantity);
                if (qtyToAdd <= 0) continue;

                // Load InventoryItem by its own ID (not ProductId)
                var inventory = await _unitOfWork.InventoryItems.GetByIdAsync(poItem.InventoryItemId);

                if (inventory is null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new OperationResult(false, ErrorMessage: "لم يتم العثور على عنصر المخزون المطلوب");
                }

                var beforeQty = inventory.Quantity;
                inventory.Quantity = MoneyPolicy.RoundToJOD(inventory.Quantity + qtyToAdd);
                inventory.MarkAsModified(userId);

                poItem.ReceivedQuantity = poItem.Quantity;
                poItem.MarkAsModified(userId);

                var movement = new InventoryMovement
                {
                    ProductId = inventory.ProductId,
                    MovementType = MovementType.Purchase,
                    Quantity = qtyToAdd,
                    BeforeQuantity = beforeQty,
                    AfterQuantity = inventory.Quantity,
                    Reason = $"Received from PO {po.OrderNumber}",
                    UserId = userId,
                    Reference = po.Id.ToString()
                };

                await _unitOfWork.InventoryItems.UpdateAsync(inventory);
                await _unitOfWork.PurchaseOrderItems.UpdateAsync(poItem);
                await _unitOfWork.InventoryMovements.AddAsync(movement);
            }

            po.Status = "Received";
            po.MarkAsModified(userId);
            await _unitOfWork.PurchaseOrders.UpdateAsync(po);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitAsync();

            await _auditService.LogAsync(userId, AuditActionType.PurchaseOrderReceived, "PurchaseOrder", purchaseOrderId, null, null, null);

            return new OperationResult(true, SuccessMessage: "تم استلام أمر الشراء بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            System.Diagnostics.Trace.TraceError("ProcessPurchaseReceivedAsync failed: {0}", ex);
            return new OperationResult(false, ErrorMessage: "حدث خطأ أثناء استلام أمر الشراء");
        }
    }

    public async Task<OperationResult> ReceivePurchaseOrderWithBatchesAsync(Guid purchaseOrderId, Guid userId, List<ReceiveBatchDto> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);
        var po = await _unitOfWork.PurchaseOrders.GetByIdAsync(purchaseOrderId);
        if (po is null)
            return new OperationResult(false, ErrorMessage: "أمر الشراء غير موجود");

        if (po.Status != "Pending")
            return new OperationResult(false, ErrorMessage: "أمر الشراء ليس في حالة انتظار");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var poItems = (await _unitOfWork.PurchaseOrderItems.FindAsync(i => i.PurchaseOrderId == purchaseOrderId)).ToList();
            var batchLookup = batches.ToLookup(b => b.InventoryItemId);

            foreach (var poItem in poItems)
            {
                var qtyToAdd = MoneyPolicy.RoundToJOD(poItem.Quantity - poItem.ReceivedQuantity);
                if (qtyToAdd <= 0) continue;

                var inventory = await _unitOfWork.InventoryItems.GetByIdAsync(poItem.InventoryItemId);
                if (inventory is null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new OperationResult(false, ErrorMessage: "لم يتم العثور على عنصر المخزون المطلوب");
                }

                var beforeQty = inventory.Quantity;
                inventory.Quantity = MoneyPolicy.RoundToJOD(inventory.Quantity + qtyToAdd);
                inventory.MarkAsModified(userId);

                poItem.ReceivedQuantity = poItem.Quantity;
                poItem.MarkAsModified(userId);

                var movement = new InventoryMovement
                {
                    ProductId = inventory.ProductId,
                    MovementType = MovementType.Purchase,
                    Quantity = qtyToAdd,
                    BeforeQuantity = beforeQty,
                    AfterQuantity = inventory.Quantity,
                    Reason = $"Received from PO {po.OrderNumber}",
                    UserId = userId,
                    Reference = po.Id.ToString()
                };

                await _unitOfWork.InventoryItems.UpdateAsync(inventory);
                await _unitOfWork.PurchaseOrderItems.UpdateAsync(poItem);

                var itemBatches = batchLookup[poItem.InventoryItemId].ToList();
                if (itemBatches.Count != 0)
                {
                    foreach (var batchDto in itemBatches)
                    {
                        var batch = new InventoryBatch
                        {
                            InventoryItemId = poItem.InventoryItemId,
                            BatchNumber = batchDto.BatchNumber,
                            ExpiryDate = batchDto.ExpiryDate,
                            ManufacturingDate = batchDto.ManufacturingDate,
                            Quantity = batchDto.Quantity,
                            UnitCost = batchDto.UnitCost,
                            ReceivedDate = DateTime.UtcNow,
                            SupplierId = po.SupplierId
                        };
                        await _unitOfWork.InventoryBatches.AddAsync(batch);
                        movement.InventoryBatchId = batch.Id;
                    }
                }
                else
                {
                    var defaultBatch = new InventoryBatch
                    {
                        InventoryItemId = poItem.InventoryItemId,
                        BatchNumber = $"PO-{po.OrderNumber}-{DateTime.UtcNow:yyyyMMdd}",
                        Quantity = qtyToAdd,
                        UnitCost = poItem.UnitCost,
                        ReceivedDate = DateTime.UtcNow,
                        SupplierId = po.SupplierId
                    };
                    await _unitOfWork.InventoryBatches.AddAsync(defaultBatch);
                    movement.InventoryBatchId = defaultBatch.Id;
                }

                await _unitOfWork.InventoryMovements.AddAsync(movement);
            }

            po.Status = "Received";
            po.MarkAsModified(userId);
            await _unitOfWork.PurchaseOrders.UpdateAsync(po);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            await _auditService.LogAsync(userId, AuditActionType.PurchaseOrderReceived, "PurchaseOrder", purchaseOrderId, null, null, null);

            return new OperationResult(true, SuccessMessage: "تم استلام أمر الشراء مع الباتشات بنجاح");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            System.Diagnostics.Trace.TraceError("ReceivePurchaseOrderWithBatchesAsync failed: {0}", ex);
            return new OperationResult(false, ErrorMessage: "حدث خطأ أثناء استلام أمر الشراء مع الباتشات");
        }
    }
}