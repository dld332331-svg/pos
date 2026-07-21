using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IKitchenOrderService
{
    Task<List<KitchenOrderDto>> GetPendingOrdersAsync();
    Task<List<string>> GetStationsAsync();
}
