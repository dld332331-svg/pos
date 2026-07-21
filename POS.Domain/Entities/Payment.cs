using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid SaleId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal TipAmount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? CardLast4 { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public Sale? Sale { get; set; }
}
