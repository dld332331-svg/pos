using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ITableService
{
    Task<List<TableDto>> GetTablesAsync();
    Task<List<RoomDto>> GetRoomsAsync();
    Task<TableDto> AddTableAsync(string name, Guid? roomId, int capacity);
    Task<OperationResult> UpdateTableStatusAsync(Guid tableId, string status);
    Task<OperationResult> OpenTableAsync(Guid tableId, Guid orderId);
    Task<OperationResult> CloseTableAsync(Guid tableId);
    Task<OperationResult> TransferOrderAsync(Guid fromTableId, Guid toTableId);
}
