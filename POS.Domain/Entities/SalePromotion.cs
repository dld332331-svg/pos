namespace POS.Domain.Entities;

public class SalePromotion : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid PromotionId { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Description { get; set; }

    public Sale? Sale { get; set; }
    public Promotion? Promotion { get; set; }
}
