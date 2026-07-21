using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IInventoryService
{
    Task<List<InventoryStatusDto>> GetCurrentStockAsync();
    Task<List<InventoryStatusDto>> GetLowStockAsync();
    Task<PagedResult<InventoryMovementDto>> GetMovementsAsync(Guid? productId, DateTime? from, DateTime? to, int page = 1, int pageSize = 20);
    Task<OperationResult> AdjustStockAsync(StockAdjustmentRequest request, Guid userId);
    Task<OperationResult> RecordWasteAsync(WasteRecordRequest request, Guid userId);
    Task<OperationResult> ProcessPurchaseReceivedAsync(Guid purchaseOrderId, Guid userId);
    Task<OperationResult> ReceivePurchaseOrderWithBatchesAsync(Guid purchaseOrderId, Guid userId, List<ReceiveBatchDto> batches);
}