namespace POS.Application.DTOs;

public record SaleItemDto(Guid? Id, Guid ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal Discount, decimal TaxRate, decimal TaxAmount, decimal LineTotal, decimal Cost, string? Notes, string? ModifierSummary, string? Unit = null, Guid? UnitOfMeasureId = null, string? UnitName = null);
public record AddItemRequest(Guid ProductId, decimal Quantity, string? Notes, List<ModifierSelectionDto>? Modifiers, string? Unit = null, Guid? UnitOfMeasureId = null);
public record ModifierSelectionDto(Guid ModifierId, Guid? ModifierSizeId, int Quantity = 1);
public record ApplyDiscountRequest(Guid SaleId, decimal DiscountAmount, string? Reason);
public record PaymentRequest(Guid SaleId, decimal Amount, string PaymentMethod, string? ReferenceNumber, Guid? CustomerId = null);
public record PaymentResult(bool Success, decimal ChangeAmount, string? ErrorMessage = null);
public record SaleSummaryDto(Guid SaleId, string InvoiceNumber, decimal SubTotal, decimal TaxAmount, decimal DiscountAmount, decimal TotalAmount, string Status, DateTime CreatedAt);
public record HeldSaleDto(Guid Id, DateTime HeldAt, string HoldReason, decimal TotalAmount);
public record AppliedPromotionDto(Guid PromotionId, string Name, decimal DiscountAmount, string? Description);