namespace POS.Application.DTOs;

public record InventoryMovementDto(Guid? Id, Guid ProductId, string ProductName, string MovementType, decimal Quantity, decimal BeforeQuantity, decimal AfterQuantity, string? Reason, string UserName, DateTime Timestamp, string? Reference, string? BatchNumber = null);
public record StockAdjustmentRequest(Guid ProductId, decimal NewQuantity, string Reason);
public record WasteRecordRequest(Guid ProductId, decimal Quantity, string Reason);
public record InventoryStatusDto(Guid ProductId, string ProductName, decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity, string Unit, decimal MinStock, bool IsLowStock);

public record InventoryBatchDto(Guid Id, Guid InventoryItemId, string BatchNumber, DateTime? ExpiryDate, DateTime? ManufacturingDate, decimal Quantity, decimal UnitCost, DateTime ReceivedDate, Guid? SupplierId);

public record ReceiveBatchDto(Guid InventoryItemId, decimal Quantity, string BatchNumber, DateTime? ExpiryDate, DateTime? ManufacturingDate, decimal UnitCost);
