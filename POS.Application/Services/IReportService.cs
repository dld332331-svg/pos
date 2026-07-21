using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IReportService
{
    Task<SalesReportDto> GetSalesReportAsync(SalesReportFilter filter);
    Task<InventoryReportDto> GetInventoryReportAsync();
    Task<ProfitabilityReportDto> GetProfitabilityReportAsync(DateTime? from, DateTime? to);
    Task<List<DailySalesDto>> GetDailySalesAsync(DateTime from, DateTime to);
}