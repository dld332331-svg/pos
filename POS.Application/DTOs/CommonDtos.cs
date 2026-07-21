namespace POS.Application.DTOs;

public record OperationResult(bool Success, string? ErrorMessage = null, string? SuccessMessage = null);
public record CategoryDto(Guid Id, string Name, Guid? ParentId, int SortOrder, bool IsActive, int ProductCount);
public record CustomerDto(Guid Id, string Name, string? Phone, string? Email, string? Address, string? Notes, decimal Balance);
public record SupplierDto(Guid Id, string Name, string? ContactPerson, string? Phone, string? Email, string? Address, decimal Balance, bool IsActive);
public record PrinterDto(Guid Id, string Name, string PrinterType, string Connection, string? IpAddress, string? Port, int PaperWidth, string AssignedRole, bool IsActive);
public record TableDto(Guid Id, string Name, string? RoomName, int Capacity, string Status, Guid? CurrentOrderId)
{
    // Backward compatibility: forms may reference TableNumber
    public string TableNumber => Name;
}
public record RoomDto(Guid Id, string Name, int SortOrder);
public record KitchenStationDto(Guid Id, string Name, bool IsActive, Guid? PrinterId, string? PrinterName);
public record AuditLogDto(DateTime Timestamp, string UserName, string ActionType, string EntityName, string? EntityId, string? BeforeValue, string? AfterValue, string? Reason);
public record BackupDto(Guid Id, string FilePath, long FileSize, DateTime CreatedAt, bool IsVerified, int RestoreCount);
public record DashboardWidgetDto(string WidgetType, string Title, string? Value, string? Description, bool IsAlert);
public record RecentTransactionDto(string InvoiceNumber, DateTime Date, decimal TotalAmount, string Status, string PaymentMethod, Guid SaleId);
