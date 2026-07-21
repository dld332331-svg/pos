using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SalesReportDto> GetSalesReportAsync(SalesReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var allSales = await _unitOfWork.Sales.GetAllAsync();
        var query = allSales
            .Where(s => s.Status == SaleStatus.Completed)
            .AsQueryable();

        if (filter.StartDate.HasValue)
            query = query.Where(s => s.CreatedAt >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            query = query.Where(s => s.CreatedAt <= filter.EndDate.Value.AddDays(1));
        if (filter.UserId.HasValue)
            query = query.Where(s => s.UserId == filter.UserId.Value);

        var filteredSales = query.ToList();

        // Get payments for payment method filter
        List<PaymentMethod> paymentMethods = new();
        if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
        {
            if (Enum.TryParse<PaymentMethod>(filter.PaymentMethod, ignoreCase: true, out var pm))
                paymentMethods.Add(pm);
        }

        Guid[]? filteredSaleIds = null;
        if (paymentMethods.Count > 0)
        {
            var allPayments = await _unitOfWork.Payments.GetAllAsync();
            filteredSaleIds = allPayments
                .Where(p => paymentMethods.Contains(p.PaymentMethod))
                .Select(p => p.SaleId)
                .Distinct()
                .ToArray();
            filteredSales = filteredSales.Where(s => filteredSaleIds.Contains(s.Id)).ToList();
        }

        // Apply category filter
        if (filter.CategoryId.HasValue)
        {
            var saleItems = await _unitOfWork.SaleItems.GetAllAsync();
            var productIdsInCategory = (await _unitOfWork.Products.FindAsync(p => p.CategoryId == filter.CategoryId.Value))
                .Select(p => p.Id)
                .ToHashSet();
            var saleIdsWithCategory = saleItems
                .Where(i => productIdsInCategory.Contains(i.ProductId))
                .Select(i => i.SaleId)
                .Distinct()
                .ToHashSet();
            filteredSales = filteredSales.Where(s => saleIdsWithCategory.Contains(s.Id)).ToList();
        }

        // Group by day
        var dailySales = filteredSales
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new DailySalesDto(
                g.Key,
                MoneyPolicy.RoundToJOD(g.Sum(s => s.TotalAmount)),
                MoneyPolicy.RoundToJOD(g.Sum(s => s.TaxAmount)),
                MoneyPolicy.RoundToJOD(g.Sum(s => s.DiscountAmount)),
                g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return new SalesReportDto(
            dailySales,
            MoneyPolicy.RoundToJOD(filteredSales.Sum(s => s.TotalAmount)),
            MoneyPolicy.RoundToJOD(filteredSales.Sum(s => s.TaxAmount)),
            MoneyPolicy.RoundToJOD(filteredSales.Sum(s => s.DiscountAmount)),
            filteredSales.Count);
    }

    public async Task<InventoryReportDto> GetInventoryReportAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventory.ToDictionary(i => i.ProductId);

        var items = new List<InventoryStatusDto>();
        int lowStockCount = 0;

        foreach (var product in products.Where(p => p.Status == ProductStatus.Active))
        {
            inventoryMap.TryGetValue(product.Id, out var inv);
            var qty = inv?.Quantity ?? 0;
            var reserved = inv?.ReservedQuantity ?? 0;
            var available = qty - reserved;
            var isLow = available <= product.MinStock;

            if (isLow) lowStockCount++;

            items.Add(new InventoryStatusDto(
                product.Id,
                product.ArabicName ?? "Unknown",
                qty,
                reserved,
                available,
                product.Unit,
                product.MinStock,
                isLow));
        }

        return new InventoryReportDto(items, lowStockCount, items.Count);
    }

    public async Task<ProfitabilityReportDto> GetProfitabilityReportAsync(DateTime? from, DateTime? to)
    {
        var sales = await _unitOfWork.Sales.GetAllAsync();
        var query = sales.Where(s => s.Status == SaleStatus.Completed);

        if (from.HasValue)
            query = query.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.CreatedAt <= to.Value.AddDays(1));

        var completedSales = query.ToList();
        var saleIds = completedSales.Select(s => s.Id).ToHashSet();

        var saleItems = (await _unitOfWork.SaleItems.GetAllAsync())
            .Where(i => saleIds.Contains(i.SaleId))
            .ToList();

        // Calculate profit per product
        var productProfit = saleItems
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                ProductName = g.First().ProductName,
                Sales = MoneyPolicy.RoundToJOD(g.Sum(i => i.LineTotal)),
                Cost = MoneyPolicy.RoundToJOD(g.Sum(i => i.Cost * i.Quantity)),
                Profit = 0m,
                Margin = 0m
            })
            .ToList();

        // Calculate profit and margin
        var profitList = productProfit.Select(pp =>
        {
            var profit = MoneyPolicy.RoundToJOD(pp.Sales - pp.Cost);
            var margin = pp.Sales > 0 ? MoneyPolicy.RoundToJOD((profit / pp.Sales) * 100) : 0;
            return new ProductProfitDto(pp.ProductName, pp.Sales, pp.Cost, profit, margin);
        })
        .OrderByDescending(p => p.Profit)
        .Take(20)
        .ToList();

        var totalSales = MoneyPolicy.RoundToJOD(completedSales.Sum(s => s.TotalAmount));
        var totalCost = MoneyPolicy.RoundToJOD(saleItems.Sum(i => i.Cost * i.Quantity));
        var grossProfit = MoneyPolicy.RoundToJOD(totalSales - totalCost);
        var profitMargin = totalSales > 0 ? MoneyPolicy.RoundToJOD((grossProfit / totalSales) * 100) : 0;

        return new ProfitabilityReportDto(totalSales, totalCost, grossProfit, profitMargin, profitList);
    }

    public async Task<List<DailySalesDto>> GetDailySalesAsync(DateTime from, DateTime to)
    {
        var sales = await _unitOfWork.Sales.GetAllAsync();

        return sales
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt.Date >= from.Date && s.CreatedAt.Date <= to.Date)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new DailySalesDto(
                g.Key,
                MoneyPolicy.RoundToJOD(g.Sum(s => s.TotalAmount)),
                MoneyPolicy.RoundToJOD(g.Sum(s => s.TaxAmount)),
                MoneyPolicy.RoundToJOD(g.Sum(s => s.DiscountAmount)),
                g.Count()))
            .OrderBy(d => d.Date)
            .ToList();
    }
}