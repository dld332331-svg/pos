using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<DashboardWidgetDto>> GetWidgetsAsync(Guid userId)
    {
        var widgets = new List<DashboardWidgetDto>();

        // 1. Today's Sales Total
        var today = DateTime.UtcNow.Date;
        var allSales = await _unitOfWork.Sales.GetAllAsync();
        var todaySales = allSales
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAt.Date == today)
            .ToList();

        var todayTotal = MoneyPolicy.RoundToJOD(todaySales.Sum(s => s.TotalAmount));
        widgets.Add(new DashboardWidgetDto(
            "metric",
            "مبيعات اليوم",
            todayTotal.ToString("F3") + " JOD",
            $"{todaySales.Count} عملية بيع",
            todayTotal == 0));

        // 2. Active Shift Info
        var currentShift = (await _unitOfWork.Shifts.FindAsync(
            s => s.UserId == userId && s.Status == ShiftStatus.Open)).FirstOrDefault();

        if (currentShift is not null)
        {
            var shiftSales = MoneyPolicy.RoundToJOD(currentShift.TotalSales);
            widgets.Add(new DashboardWidgetDto(
                "info",
                "الوردية الحالية",
                $"مبيعات: {shiftSales.ToString("F3")} JOD",
                $"تم الفتح: {currentShift.OpenedAt:HH:mm}",
                false));
        }
        else
        {
            widgets.Add(new DashboardWidgetDto(
                "warning",
                "الوردية",
                "لا توجد وردية مفتوحة",
                "افتح وردية لبدء البيع",
                true));
        }

        // 3. Low Stock Count
        var products = await _unitOfWork.Products.GetAllAsync();
        var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventory.ToDictionary(i => i.ProductId);

        var lowStockCount = products
            .Where(p => p.Status == ProductStatus.Active)
            .Count(p =>
            {
                inventoryMap.TryGetValue(p.Id, out var inv);
                var available = inv?.AvailableQuantity ?? 0;
                return available <= p.MinStock;
            });

        widgets.Add(new DashboardWidgetDto(
            lowStockCount > 0 ? "alert" : "metric",
            "مخزون منخفض",
            lowStockCount.ToString(),
            lowStockCount > 0 ? "منتجات تحت الحد الأدنى" : "المخزون جيد",
            lowStockCount > 0));

        // 4. Pending Kitchen Orders
        var activeSales = allSales
            .Where(s => s.Status is SaleStatus.Active or SaleStatus.Held)
            .ToList();

        var activeSaleIds = activeSales.Select(s => s.Id).ToHashSet();
        var allSaleItems = await _unitOfWork.SaleItems.GetAllAsync();
        var pendingKitchenItems = allSaleItems
            .Where(i => activeSaleIds.Contains(i.SaleId))
            .ToList();

        var kitchenProductIds = products
            .Where(p => p.KitchenStationId is not null)
            .Select(p => p.Id)
            .ToHashSet();

        var kitchenItemCount = pendingKitchenItems
            .Count(i => kitchenProductIds.Contains(i.ProductId));


        widgets.Add(new DashboardWidgetDto(
            kitchenItemCount > 0 ? "alert" : "metric",
            "طلبات المطبخ",
            kitchenItemCount.ToString(),
            kitchenItemCount > 0 ? "طلبات قيد التحضير" : "لا توجد طلبات معلقة",
            kitchenItemCount > 0));

        // 5. Recent Transactions (last 5 completed sales)
        var recentSales = allSales
            .Where(s => s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToList();

        var recentDescriptions = recentSales.Count > 0
            ? string.Join("\n", recentSales.Select(s => $"{s.InvoiceNumber}: {s.TotalAmount:F3} JOD"))
            : "لا توجد معاملات حديثة";

        widgets.Add(new DashboardWidgetDto(
            "list",
            "آخر المعاملات",
            recentSales.Count.ToString(),
            recentDescriptions,
            false));

        return widgets;
    }

    public async Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int count = 5)
    {
        var allSales = await _unitOfWork.Sales.GetAllAsync();
        var recent = allSales
            .Where(s => s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToList();

        var saleIds = recent.Select(s => s.Id).ToHashSet();
        var payments = (await _unitOfWork.Payments.GetAllAsync())
            .Where(p => saleIds.Contains(p.SaleId))
            .GroupBy(p => p.SaleId)
            .ToDictionary(g => g.Key, g => g.First().PaymentMethod.ToString());

        return recent.Select(s => new RecentTransactionDto(
            InvoiceNumber: s.InvoiceNumber ?? "",
            Date: s.CreatedAt,
            TotalAmount: s.TotalAmount,
            Status: s.Status.ToString(),
            PaymentMethod: payments.TryGetValue(s.Id, out var pm) ? pm : "—",
            SaleId: s.Id
        )).ToList();
    }
}