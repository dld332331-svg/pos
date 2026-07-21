using System.Text.Json;
using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public PromotionService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<List<PromotionDto>> GetAllAsync()
    {
        var promotions = await _unitOfWork.Promotions.GetAllAsync();
        return promotions.OrderBy(p => p.Priority).Select(MapToDto).ToList();
    }

    public async Task<PromotionDto?> GetByIdAsync(Guid id)
    {
        var promotion = await _unitOfWork.Promotions.GetByIdAsync(id);
        return promotion == null ? null : MapToDto(promotion);
    }

    public async Task<PromotionDto> CreateAsync(CreatePromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("اسم العرض الترويجي مطلوب");

        if (request.EndDate <= request.StartDate)
            throw new ArgumentException("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");

        var promotion = new Promotion
        {
            Name = request.Name,
            Description = request.Description,
            Type = Enum.Parse<PromotionType>(request.Type),
            Value = request.Value,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            IsActive = true,
            Priority = 0,
            MinPurchaseAmount = request.MinPurchaseAmount,
            MinQuantity = request.MinQuantity,
            BuyQuantity = request.BuyQuantity,
            FreeQuantity = request.FreeQuantity,
            MaxApplications = request.MaxApplications,
            ApplicableProductIdsJson = request.ApplicableProductIdsJson,
            ApplicableCategoryIdsJson = request.ApplicableCategoryIdsJson
        };

        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.SettingChanged, "Promotion", promotion.Id,
            null, $"Created: {promotion.Name}", "تم إنشاء عرض ترويجي جديد");

        return MapToDto(promotion);
    }

    public async Task<PromotionDto> UpdateAsync(UpdatePromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var promotion = await _unitOfWork.Promotions.GetByIdAsync(request.Id)
            ?? throw new InvalidOperationException("العرض الترويجي غير موجود");

        var beforeValue = $"Name={promotion.Name},Active={promotion.IsActive}";

        promotion.Name = request.Name;
        promotion.Description = request.Description;
        promotion.Type = Enum.Parse<PromotionType>(request.Type);
        promotion.Value = request.Value;
        promotion.StartDate = request.StartDate.ToUniversalTime();
        promotion.EndDate = request.EndDate.ToUniversalTime();
        promotion.IsActive = request.IsActive;
        promotion.Priority = request.Priority;
        promotion.MinPurchaseAmount = request.MinPurchaseAmount;
        promotion.MinQuantity = request.MinQuantity;
        promotion.BuyQuantity = request.BuyQuantity;
        promotion.FreeQuantity = request.FreeQuantity;
        promotion.MaxApplications = request.MaxApplications;
        promotion.ApplicableProductIdsJson = request.ApplicableProductIdsJson;
        promotion.ApplicableCategoryIdsJson = request.ApplicableCategoryIdsJson;
        promotion.MarkAsModified();

        await _unitOfWork.Promotions.UpdateAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.SettingChanged, "Promotion", request.Id,
            beforeValue, $"Updated: {promotion.Name}", "تم تحديث العرض الترويجي");

        return MapToDto(promotion);
    }

    public async Task DeleteAsync(Guid id)
    {
        var promotion = await _unitOfWork.Promotions.GetByIdAsync(id)
            ?? throw new InvalidOperationException("العرض الترويجي غير موجود");

        promotion.MarkAsDeleted();
        await _unitOfWork.Promotions.UpdateAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.SettingChanged, "Promotion", id,
            null, $"Deleted: {promotion.Name}", "تم حذف العرض الترويجي");
    }

    public async Task<List<PromotionResultDto>> GetEligiblePromotionsAsync(Guid saleId, List<SaleItemDto> items)
    {
        var now = DateTime.UtcNow;
        var promotions = (await _unitOfWork.Promotions.GetAllAsync())
            .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
            .OrderBy(p => p.Priority)
            .ToList();

        var subTotal = items.Sum(i => i.UnitPrice * i.Quantity);
        var results = new List<PromotionResultDto>();

        foreach (var promo in promotions)
        {
            if (promo.MinPurchaseAmount.HasValue && subTotal < promo.MinPurchaseAmount.Value)
                continue;

            var discount = CalculatePromotionDiscount(promo, items, subTotal);
            if (discount > 0)
            {
                results.Add(new PromotionResultDto(
                    promo.Id, promo.Name, MoneyPolicy.RoundToJOD(discount),
                    promo.Description));
            }
        }

        return results;
    }

    public async Task<PromotionResultDto?> ApplyPromotionAsync(Guid saleId, Guid promotionId, List<SaleItemDto> items)
    {
        var sale = await _unitOfWork.Sales.GetByIdAsync(saleId)
            ?? throw new InvalidOperationException("الفاتورة غير موجودة");

        var promotion = await _unitOfWork.Promotions.GetByIdAsync(promotionId)
            ?? throw new InvalidOperationException("العرض الترويجي غير موجود");

        if (!promotion.IsActive || promotion.StartDate > DateTime.UtcNow || promotion.EndDate < DateTime.UtcNow)
            throw new InvalidOperationException("العرض الترويجي غير نشط أو منتهي الصلاحية");

        if (promotion.MinPurchaseAmount.HasValue)
        {
            var subTotal = items.Sum(i => i.UnitPrice * i.Quantity);
            if (subTotal < promotion.MinPurchaseAmount.Value)
                throw new InvalidOperationException($"الحد الأدنى للشراء هو {promotion.MinPurchaseAmount.Value} د.أ");
        }

        var discount = CalculatePromotionDiscount(promotion, items, items.Sum(i => i.UnitPrice * i.Quantity));
        if (discount <= 0)
            return null;

        discount = MoneyPolicy.RoundToJOD(discount);

        var salePromotion = new SalePromotion
        {
            SaleId = saleId,
            PromotionId = promotionId,
            DiscountAmount = discount,
            Description = $"{promotion.Name}: {discount} د.أ"
        };

        sale.ApplyPromotion(salePromotion);
        sale.DiscountAmount += discount;
        sale.TotalAmount = MoneyPolicy.RoundToJOD(sale.SubTotal + sale.TaxAmount - sale.DiscountAmount);

        await _unitOfWork.SalePromotions.AddAsync(salePromotion);
        await _unitOfWork.SaveChangesAsync();

        return new PromotionResultDto(promotionId, promotion.Name, discount, promotion.Description);
    }

    private static decimal CalculatePromotionDiscount(Promotion promo, List<SaleItemDto> items, decimal subTotal)
    {
        var applicableItems = GetApplicableItems(promo, items);
        var applicableSubTotal = applicableItems.Sum(i => i.UnitPrice * i.Quantity);

        return promo.Type switch
        {
            PromotionType.Percentage => subTotal * promo.Value / 100m,
            PromotionType.FixedAmount => Math.Min(promo.Value, subTotal),
            PromotionType.BuyXGetY when promo.BuyQuantity > 0 && promo.FreeQuantity > 0 =>
                CalculateBuyXGetY(promo, applicableItems),
            PromotionType.MultiBuy when promo.MinQuantity > 0 =>
                CalculateMultiBuy(promo, applicableItems, applicableSubTotal),
            _ => 0
        };
    }

    private static List<SaleItemDto> GetApplicableItems(Promotion promo, List<SaleItemDto> items)
    {
        var productIds = !string.IsNullOrWhiteSpace(promo.ApplicableProductIdsJson)
            ? JsonSerializer.Deserialize<HashSet<Guid>>(promo.ApplicableProductIdsJson) ?? new()
            : null;

        if (productIds != null && productIds.Count > 0)
            return items.Where(i => productIds.Contains(i.ProductId)).ToList();

        return items;
    }

    private static decimal CalculateBuyXGetY(Promotion promo, List<SaleItemDto> items)
    {
        if (items.Count == 0) return 0;

        var totalQty = items.Sum(i => i.Quantity);
        var unitPrice = items.Min(i => i.UnitPrice);

        var freeSets = (int)(totalQty / (promo.BuyQuantity!.Value + promo.FreeQuantity!.Value));
        if (freeSets <= 0)
        {
            freeSets = (int)(totalQty / promo.BuyQuantity!.Value);
            if (freeSets <= 0) return 0;
            return freeSets * promo.FreeQuantity!.Value * unitPrice;
        }

        return freeSets * promo.FreeQuantity!.Value * unitPrice;
    }

    private static decimal CalculateMultiBuy(Promotion promo, List<SaleItemDto> items, decimal applicableSubTotal)
    {
        var totalQty = items.Sum(i => i.Quantity);
        if (totalQty < promo.MinQuantity!.Value) return 0;

        return applicableSubTotal * promo.Value / 100m;
    }

    private static PromotionDto MapToDto(Promotion p) => new(
        p.Id, p.Name, p.Description, p.Type.ToString(), p.Value,
        p.StartDate, p.EndDate, p.IsActive, p.Priority,
        p.MinPurchaseAmount, p.MinQuantity, p.BuyQuantity, p.FreeQuantity,
        p.MaxApplications, p.ApplicableProductIdsJson, p.ApplicableCategoryIdsJson);
}
