namespace POS.Application.DTOs;

public record PromotionDto(Guid Id, string Name, string? Description, string Type, decimal Value,
    DateTime StartDate, DateTime EndDate, bool IsActive, int Priority,
    decimal? MinPurchaseAmount, int? MinQuantity, int? BuyQuantity, int? FreeQuantity,
    int MaxApplications, string? ApplicableProductIdsJson, string? ApplicableCategoryIdsJson);

public record CreatePromotionRequest(string Name, string? Description, string Type, decimal Value,
    DateTime StartDate, DateTime EndDate, decimal? MinPurchaseAmount = null,
    int? MinQuantity = null, int? BuyQuantity = null, int? FreeQuantity = null,
    int MaxApplications = 99, string? ApplicableProductIdsJson = null,
    string? ApplicableCategoryIdsJson = null);

public record UpdatePromotionRequest(Guid Id, string Name, string? Description, string Type, decimal Value,
    DateTime StartDate, DateTime EndDate, bool IsActive, int Priority,
    decimal? MinPurchaseAmount = null, int? MinQuantity = null, int? BuyQuantity = null,
    int? FreeQuantity = null, int MaxApplications = 99,
    string? ApplicableProductIdsJson = null, string? ApplicableCategoryIdsJson = null);

public record ApplyPromotionRequest(Guid SaleId, Guid PromotionId);

public record PromotionResultDto(Guid PromotionId, string Name, decimal DiscountAmount, string? Description);
