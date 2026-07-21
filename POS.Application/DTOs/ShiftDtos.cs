namespace POS.Application.DTOs;

public record OpenShiftRequest(decimal OpeningCash, Guid RegisterId);
public record CloseShiftRequest(Guid ShiftId, decimal ActualCash);
public record ShiftDto(Guid Id, int ShiftNumber, string UserName, string RegisterName, decimal OpeningCash, decimal? ClosingCash, decimal TotalSales, decimal TotalReturns, decimal TotalExpenses, decimal? ExpectedCash, decimal? ActualCash, decimal? Variance, string Status, DateTime OpenedAt, DateTime? ClosedAt)
{
    public decimal TotalCashSales => TotalSales;
}
public record ShiftSummaryDto(decimal TotalCashSales, decimal TotalCardSales, decimal TotalSales, int TotalTransactions, int TotalReturns);
