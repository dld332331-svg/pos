using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Sale : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid ShiftId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public OrderType OrderType { get; set; }
    public Guid? TableId { get; set; }
    public Guid RegisterId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RoundAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Active;
    public string? Notes { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public Table? Table { get; set; }
    public Customer? Customer { get; set; }
    public Register? Register { get; set; }
    public Shift? Shift { get; set; }
    private readonly List<SaleItem> _saleItems = new();
    public IReadOnlyCollection<SaleItem> SaleItems => _saleItems.AsReadOnly();
    private readonly List<Payment> _payments = new();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
    private readonly List<SalePromotion> _appliedPromotions = new();
    public IReadOnlyCollection<SalePromotion> AppliedPromotions => _appliedPromotions.AsReadOnly();

    public void AddItem(SaleItem item) => _saleItems.Add(item);
    public void RemoveItem(SaleItem item) => _saleItems.Remove(item);
    public void AddPayment(Payment payment) => _payments.Add(payment);
    public void ApplyPromotion(SalePromotion sp) => _appliedPromotions.Add(sp);
}
