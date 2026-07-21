using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IPromotionService
{
    Task<List<PromotionDto>> GetAllAsync();
    Task<PromotionDto?> GetByIdAsync(Guid id);
    Task<PromotionDto> CreateAsync(CreatePromotionRequest request);
    Task<PromotionDto> UpdateAsync(UpdatePromotionRequest request);
    Task DeleteAsync(Guid id);
    Task<List<PromotionResultDto>> GetEligiblePromotionsAsync(Guid saleId, List<SaleItemDto> items);
    Task<PromotionResultDto?> ApplyPromotionAsync(Guid saleId, Guid promotionId, List<SaleItemDto> items);
}
