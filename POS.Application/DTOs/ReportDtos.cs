namespace POS.Application.DTOs;

public record SalesReportFilter(DateTime? StartDate, DateTime? EndDate, Guid? UserId, Guid? CategoryId, string? PaymentMethod);
public record SalesReportDto(List<DailySalesDto> DailySales, decimal GrandTotal, decimal GrandTax, decimal GrandDiscount, int TotalTransactions);
public record DailySalesDto(DateTime Date, decimal TotalSales, decimal TotalTax, decimal TotalDiscount, int TransactionCount);
public record InventoryReportDto(List<InventoryStatusDto> Items, int LowStockCount, int TotalItems);
public record ProfitabilityReportDto(decimal TotalSales, decimal TotalCost, decimal GrossProfit, decimal ProfitMargin, List<ProductProfitDto> TopProducts);
public record ProductProfitDto(string ProductName, decimal Sales, decimal Cost, decimal Profit, decimal Margin);
public record CashReportDto(decimal ExpectedCash, decimal ActualCash, decimal Variance, decimal TotalCashPayments, decimal TotalCardPayments, decimal TotalExpenses, decimal TotalWithdrawals, decimal TotalDeposits);