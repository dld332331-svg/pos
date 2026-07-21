using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PromotionType Type { get; set; }
    public decimal Value { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public decimal? MinPurchaseAmount { get; set; }
    public string? ApplicableProductIdsJson { get; set; }
    public string? ApplicableCategoryIdsJson { get; set; }
    public int? MinQuantity { get; set; }
    public int? BuyQuantity { get; set; }
    public int? FreeQuantity { get; set; }
    public int MaxApplications { get; set; } = 99;
}
