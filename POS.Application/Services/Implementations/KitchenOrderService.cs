using POS.Application.DTOs;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class KitchenOrderService : IKitchenOrderService
{
    private readonly IUnitOfWork _unitOfWork;

    public KitchenOrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<KitchenOrderDto>> GetPendingOrdersAsync()
    {
        var allSales = await _unitOfWork.Sales.GetAllAsync();
        var tables = (await _unitOfWork.Tables.GetAllAsync()).ToDictionary(t => t.Id);

        var activeSales = allSales
            .Where(s => s.Status is SaleStatus.Active or SaleStatus.Held)
            .ToList();

        if (activeSales.Count == 0)
            return new List<KitchenOrderDto>();

        var allItems = await _unitOfWork.SaleItems.GetAllAsync();
        var stations = (await _unitOfWork.KitchenStations.GetAllAsync()).ToDictionary(s => s.Id);

        var activeSaleIds = activeSales.Select(s => s.Id).ToHashSet();
        var kitchenItems = allItems
            .Where(i => activeSaleIds.Contains(i.SaleId) && i.KitchenStationId != null)
            .GroupBy(i => i.SaleId)
            .ToList();

        var orders = new List<KitchenOrderDto>();

        foreach (var itemGroup in kitchenItems)
        {
            var sale = activeSales.FirstOrDefault(s => s.Id == itemGroup.Key);
            if (sale == null) continue;

            var saleItemList = itemGroup.ToList();
            var firstItem = saleItemList.First();
            var stationName = firstItem.KitchenStationId != null && stations.TryGetValue(firstItem.KitchenStationId.Value, out var station)
                ? station.Name
                : "المطبخ الرئيسي";

            var orderTime = sale.CreatedAt;
            var isPriority = (DateTime.UtcNow - orderTime).TotalMinutes > 30;

            string tableOrType = sale.OrderType switch
            {
                OrderType.DineIn when sale.TableId != null && tables.TryGetValue(sale.TableId.Value, out var table) => $"طاولة {table.Name}",
                OrderType.DineIn => "طاولة",
                OrderType.Takeaway => "سفري",
                OrderType.Delivery => "توصيل",
                _ => "---"
            };

            orders.Add(new KitchenOrderDto(
                sale.InvoiceNumber,
                orderTime,
                tableOrType,
                stationName,
                isPriority,
                sale.Notes,
                saleItemList.Select(i => new KitchenItemDto(
                    i.ProductArabicName ?? i.ProductName,
                    i.Quantity,
                    i.ModifierSummary
                )).ToList()
            ));
        }

        return orders.OrderBy(o => o.OrderTime).ToList();
    }

    public async Task<List<string>> GetStationsAsync()
    {
        var stations = await _unitOfWork.KitchenStations.GetAllAsync();
        return stations.Where(s => s.IsActive).Select(s => s.Name).ToList();
    }
}
