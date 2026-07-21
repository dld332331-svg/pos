using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ISaleService
{
    Task<Guid> CreateNewSaleAsync(Guid userId, Guid shiftId, string? orderType = null, Guid? tableId = null);
    Task AddItemAsync(Guid saleId, AddItemRequest request);
    Task RemoveItemAsync(Guid saleId, Guid itemId);
    Task UpdateItemQuantityAsync(Guid saleId, Guid itemId, decimal newQuantity);
    Task<SaleItemDto> ModifyItemAsync(Guid saleId, Guid itemId, ModifierSelectionDto[] modifiers);
    Task ApplyDiscountAsync(ApplyDiscountRequest request);
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
    Task<SaleSummaryDto> GetSaleSummaryAsync(Guid saleId);
    Task<List<SaleItemDto>> GetSaleItemsAsync(Guid saleId);
    Task<Guid> HoldSaleAsync(Guid saleId, string reason);
    Task<SaleSummaryDto> RetrieveHeldSaleAsync(Guid heldSaleId);
    Task<List<HeldSaleDto>> GetHeldSalesAsync(Guid shiftId);
    Task<OperationResult> CancelSaleAsync(Guid saleId, string reason);
    Task<OperationResult> ReturnItemsAsync(Guid originalSaleId, List<ReturnItemRequest> items, string reason);
    Task<List<SaleSummaryDto>> GetSalesHistoryAsync(DateTime? from, DateTime? to, int page = 1, int pageSize = 20);
    Task<List<AppliedPromotionDto>> GetAppliedPromotionsAsync(Guid saleId);
}

public record ReturnItemRequest(Guid SaleItemId, decimal Quantity, string Reason);