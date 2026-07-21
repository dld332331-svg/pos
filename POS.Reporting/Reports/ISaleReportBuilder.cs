namespace POS.Reporting.Reports;

#region Data Transfer Records for Sales Reports

public record HourlySalesDto(int Hour, decimal TotalSales, int TransactionCount, decimal AverageTicket);
public record PaymentMethodSalesDto(string MethodName, decimal TotalAmount, int TransactionCount, decimal Percentage);
public record ProductSalesRankDto(string ProductName, decimal QuantitySold, decimal TotalRevenue, int TransactionCount);
public record CategorySalesDto(string CategoryName, int QuantitySold, decimal TotalRevenue, decimal AveragePrice, int TransactionCount);
public record UserSalesDto(string UserName, int TransactionCount, decimal TotalSales, decimal AverageTicket, decimal TotalRefunds, int RefundCount);
public record RefundSummaryDto(string TransactionId, decimal Amount, string Reason, DateTime Time, string ProductName);
public record DailySalesReportData(
    List<HourlySalesDto> HourlySales,
    List<PaymentMethodSalesDto> PaymentBreakdown,
    List<ProductSalesRankDto> TopProducts,
    List<RefundSummaryDto> Refunds,
    decimal GrandTotal,
    decimal GrandTax,
    decimal GrandDiscount,
    int TotalTransactions,
    decimal NetSales);
public record SalesByCategoryReportData(
    List<CategorySalesDto> Categories,
    decimal GrandTotal,
    int TotalTransactions);
public record SalesByUserReportData(
    List<UserSalesDto> Users,
    decimal GrandTotal,
    int TotalTransactions);
public record SalesByPaymentMethodReportData(
    List<PaymentMethodSalesDto> Methods,
    decimal GrandTotal,
    int TotalTransactions);

#endregion

/// <summary>
/// Interface for building various sales reports with comprehensive data.
/// </summary>
public interface ISaleReportBuilder
{
    byte[] BuildDailySalesReport(DateTime date, string businessName, DailySalesReportData data);
    byte[] BuildSalesByCategoryReport(DateTime from, DateTime to, string businessName, SalesByCategoryReportData data);
    byte[] BuildSalesByUserReport(DateTime from, DateTime to, string businessName, SalesByUserReportData data);
    byte[] BuildSalesByPaymentMethodReport(DateTime from, DateTime to, string businessName, SalesByPaymentMethodReportData data);
}