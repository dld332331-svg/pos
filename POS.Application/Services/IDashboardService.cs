using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IDashboardService
{
    Task<List<DashboardWidgetDto>> GetWidgetsAsync(Guid userId);

    /// <summary>
    /// Returns the most recent completed sales for the dashboard grid (§14).
    /// </summary>
    Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int count = 5);
}