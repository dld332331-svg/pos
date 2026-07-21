using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IInventoryService _inventoryService;

    public PurchaseOrderService(IUnitOfWork unitOfWork, IAuditService auditService, IInventoryService inventoryService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _inventoryService = inventoryService;
    }

    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(Guid supplierId, Guid userId, List<PurchaseOrderItemDto> items, string? notes)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(supplierId)
            ?? throw new InvalidOperationException("المورد غير موجود");

        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new InvalidOperationException("يجب إضافة بند واحد على الأقل");

        var orderNumber = await GenerateOrderNumberAsync();

        var purchaseOrder = new PurchaseOrder
        {
            SupplierId = supplierId,
            OrderNumber = orderNumber,
            Status = "Pending",
            Notes = notes,
            UserId = userId,
            TotalAmount = 0
        };

        await _unitOfWork.PurchaseOrders.AddAsync(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        decimal totalAmount = 0;
        foreach (var item in items)
        {
            var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(item.InventoryItemId);
            var itemName = inventoryItem?.Name ?? item.ItemName;

            var unitCost = MoneyPolicy.RoundToJOD(item.UnitCost);
            var totalCost = MoneyPolicy.RoundToJOD(item.Quantity * unitCost);
            totalAmount += totalCost;

            var poItem = new PurchaseOrderItem
            {
                PurchaseOrderId = purchaseOrder.Id,
                InventoryItemId = item.InventoryItemId,
                ItemName = itemName,
                Quantity = item.Quantity,
                UnitCost = unitCost,
                TotalCost = totalCost,
                ReceivedQuantity = 0
            };
            await _unitOfWork.PurchaseOrderItems.AddAsync(poItem);
        }

        purchaseOrder.TotalAmount = MoneyPolicy.RoundToJOD(totalAmount);
        await _unitOfWork.PurchaseOrders.UpdateAsync(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(userId, AuditActionType.SettingChanged, "PurchaseOrder", purchaseOrder.Id,
            null, $"Supplier={supplier.Name},Total={totalAmount},Items={items.Count}", notes);

        return await MapToDtoAsync(purchaseOrder);
    }

    public async Task<PurchaseOrderDto?> GetPurchaseOrderAsync(Guid purchaseOrderId)
    {
        var po = await _unitOfWork.PurchaseOrders.GetByIdAsync(purchaseOrderId);
        return po is null ? null : await MapToDtoAsync(po);
    }

    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(string? status = null)
    {
        var allOrders = await _unitOfWork.PurchaseOrders.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(status))
            allOrders = allOrders.Where(po => po.Status == status).ToList();

        var result = new List<PurchaseOrderDto>();
        foreach (var po in allOrders.OrderByDescending(po => po.CreatedAt))
        {
            result.Add(await MapToDtoAsync(po));
        }
        return result;
    }

    public async Task<OperationResult> UpdatePurchaseOrderStatusAsync(Guid purchaseOrderId, string status)
    {
        var po = await _unitOfWork.PurchaseOrders.GetByIdAsync(purchaseOrderId);
        if (po is null)
            return new OperationResult(false, ErrorMessage: "أمر الشراء غير موجود");

        po.Status = status;
        po.MarkAsModified();
        await _unitOfWork.PurchaseOrders.UpdateAsync(po);
        await _unitOfWork.SaveChangesAsync();

        return new OperationResult(true, SuccessMessage: "تم تحديث حالة أمر الشراء بنجاح");
    }

    public async Task<OperationResult> ReceivePurchaseOrderAsync(Guid purchaseOrderId, Guid userId)
    {
        // Delegate inventory processing to the InventoryService which handles
        // stock updates, movements, and transactions
        return await _inventoryService.ProcessPurchaseReceivedAsync(purchaseOrderId, userId);
    }

    private async Task<string> GenerateOrderNumberAsync()
    {
        var allOrders = await _unitOfWork.PurchaseOrders.GetAllAsync();
        var maxNum = allOrders
            .Select(po => po.OrderNumber)
            .Where(n => !string.IsNullOrEmpty(n) && n.StartsWith("PO-"))
            .Select(n => int.TryParse(n[3..], out var num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"PO-{(maxNum + 1):D3}";
    }

    private async Task<PurchaseOrderDto> MapToDtoAsync(PurchaseOrder po)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(po.SupplierId);
        var poItems = await _unitOfWork.PurchaseOrderItems.FindAsync(i => i.PurchaseOrderId == po.Id);

        var itemDtos = poItems.Select(i => new PurchaseOrderItemDto(
            i.InventoryItemId,
            i.ItemName ?? "Unknown",
            i.Quantity,
            i.UnitCost,
            i.TotalCost,
            i.ReceivedQuantity)).ToList();

        return new PurchaseOrderDto(
            po.Id,
            po.OrderNumber,
            supplier?.Name ?? "Unknown",
            po.TotalAmount,
            po.Status,
            po.CreatedAt,
            itemDtos,
            po.Notes);
    }
}
