using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class TableService : ITableService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public TableService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<List<TableDto>> GetTablesAsync()
    {
        var tables = await _unitOfWork.Tables.GetAllAsync();
        var rooms = await _unitOfWork.Rooms.GetAllAsync();
        var roomMap = rooms.ToDictionary(r => r.Id, r => r.Name);

        return tables.Select(t => new TableDto(
            t.Id,
            t.Name,
            roomMap.TryGetValue(t.RoomId, out var rn) ? rn : null,
            t.Capacity,
            t.Status.ToString(),
            t.CurrentOrderId)).ToList();
    }

    public async Task<List<RoomDto>> GetRoomsAsync()
    {
        var rooms = await _unitOfWork.Rooms.GetAllAsync();
        return rooms
            .OrderBy(r => r.SortOrder)
            .Select(r => new RoomDto(r.Id, r.Name, r.SortOrder))
            .ToList();
    }

    public async Task<TableDto> AddTableAsync(string name, Guid? roomId, int capacity)
    {
        if (roomId.HasValue)
        {
            var existingInRoom = (await _unitOfWork.Tables.FindAsync(
                t => t.RoomId == roomId.Value && t.Name == name)).FirstOrDefault();

            if (existingInRoom is not null)
                throw new InvalidOperationException("رقم الطاولة موجود بالفعل في هذه الغرفة");
        }

        var table = new Table
        {
            Name = name,
            RoomId = roomId ?? Guid.Empty,
            Capacity = capacity,
            Status = TableStatus.Available
        };

        await _unitOfWork.Tables.AddAsync(table);
        await _unitOfWork.SaveChangesAsync();

        string? roomName = null;
        if (roomId.HasValue)
        {
            var room = await _unitOfWork.Rooms.GetByIdAsync(roomId.Value);
            roomName = room?.Name;
        }

        return new TableDto(table.Id, table.Name, roomName, table.Capacity, "Available", null);
    }

    public async Task<OperationResult> UpdateTableStatusAsync(Guid tableId, string status)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
        if (table is null)
            return new OperationResult(false, ErrorMessage: "الطاولة غير موجودة");

        if (!Enum.TryParse<TableStatus>(status, ignoreCase: true, out var newStatus))
            return new OperationResult(false, ErrorMessage: "حالة غير صالحة");

        var oldStatus = table.Status;
        table.Status = newStatus;
        table.MarkAsModified();

        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();

        return new OperationResult(true, SuccessMessage: $"تم تغيير حالة الطاولة من {oldStatus} إلى {newStatus}");
    }

    public async Task<OperationResult> OpenTableAsync(Guid tableId, Guid orderId)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
        if (table is null)
            return new OperationResult(false, ErrorMessage: "الطاولة غير موجودة");

        if (table.Status is not TableStatus.Available and not TableStatus.Reserved)
            return new OperationResult(false, ErrorMessage: "الطاولة ليست متاحة");

        table.Status = TableStatus.Occupied;
        table.CurrentOrderId = orderId;
        table.MarkAsModified();

        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();

        return new OperationResult(true, SuccessMessage: "تم فتح الطاولة بنجاح");
    }

    public async Task<OperationResult> CloseTableAsync(Guid tableId)
    {
        var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
        if (table is null)
            return new OperationResult(false, ErrorMessage: "الطاولة غير موجودة");

        if (table.Status is TableStatus.Available)
            return new OperationResult(false, ErrorMessage: "الطاولة مفتوحة بالفعل");

        table.Status = TableStatus.Available;
        table.CurrentOrderId = null;
        table.MarkAsModified();

        await _unitOfWork.Tables.UpdateAsync(table);
        await _unitOfWork.SaveChangesAsync();

        return new OperationResult(true, SuccessMessage: "تم إغلاق الطاولة بنجاح");
    }

    public async Task<OperationResult> TransferOrderAsync(Guid fromTableId, Guid toTableId)
    {
        if (fromTableId == toTableId)
            return new OperationResult(false, ErrorMessage: "لا يمكن نقل الطلب إلى نفس الطاولة");

        var fromTable = await _unitOfWork.Tables.GetByIdAsync(fromTableId);
        if (fromTable is null)
            return new OperationResult(false, ErrorMessage: "الطاولة المصدر غير موجودة");

        var toTable = await _unitOfWork.Tables.GetByIdAsync(toTableId);
        if (toTable is null)
            return new OperationResult(false, ErrorMessage: "الطاولة الوجهة غير موجودة");

        if (fromTable.CurrentOrderId is null)
            return new OperationResult(false, ErrorMessage: "لا يوجد طلب على الطاولة المصدر");

        if (toTable.Status is not TableStatus.Available and not TableStatus.Reserved)
            return new OperationResult(false, ErrorMessage: "الطاولة الوجهة غير متاحة");

        var orderId = fromTable.CurrentOrderId.Value;

        // Move order to destination table
        toTable.Status = TableStatus.Occupied;
        toTable.CurrentOrderId = orderId;
        toTable.MarkAsModified();

        // Clear source table
        fromTable.Status = TableStatus.Available;
        fromTable.CurrentOrderId = null;
        fromTable.MarkAsModified();

        // Update sale's table reference
        var sale = (await _unitOfWork.Sales.FindAsync(s => s.Id == orderId)).FirstOrDefault();
        if (sale is not null)
        {
            sale.TableId = toTableId;
            sale.MarkAsModified();
            await _unitOfWork.Sales.UpdateAsync(sale);
        }

        await _unitOfWork.Tables.UpdateAsync(fromTable);
        await _unitOfWork.Tables.UpdateAsync(toTable);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.PriceChange, "Table", fromTableId,
            $"OrderId={orderId},Table={fromTableId}", $"OrderId={orderId},Table={toTableId}", "Order transferred between tables");

        return new OperationResult(true, SuccessMessage: "تم نقل الطلب بنجاح");
    }
}