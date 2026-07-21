using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

// ─── Table Commands ────────────────────────────────────────────────────────

public record CreateTableCommand(string Name, Guid? RoomId, int Capacity) : ICommand<TableDto>;
public record OpenTableCommand(Guid TableId, Guid OrderId) : ICommand<OperationResult>;
public record CloseTableCommand(Guid TableId) : ICommand<OperationResult>;
public record TransferOrderCommand(Guid FromTableId, Guid ToTableId) : ICommand<OperationResult>;

public sealed class CreateTableCommandHandler(ITableService service) : ICommandHandler<CreateTableCommand, TableDto>
{
    public Task<TableDto> HandleAsync(CreateTableCommand cmd, CancellationToken ct = default)
        => service.AddTableAsync(cmd.Name, cmd.RoomId, cmd.Capacity);
}

public sealed class OpenTableCommandHandler(ITableService service) : ICommandHandler<OpenTableCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(OpenTableCommand cmd, CancellationToken ct = default)
        => service.OpenTableAsync(cmd.TableId, cmd.OrderId);
}

public sealed class CloseTableCommandHandler(ITableService service) : ICommandHandler<CloseTableCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(CloseTableCommand cmd, CancellationToken ct = default)
        => service.CloseTableAsync(cmd.TableId);
}

public sealed class TransferOrderCommandHandler(ITableService service) : ICommandHandler<TransferOrderCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(TransferOrderCommand cmd, CancellationToken ct = default)
        => service.TransferOrderAsync(cmd.FromTableId, cmd.ToTableId);
}

// ─── Customer Commands ─────────────────────────────────────────────────────

public record CreateCustomerCommand(string Name, string? Phone, string? Email) : ICommand<CustomerDto>;
public record UpdateCustomerCommand(Guid Id, string Name, string? Phone, string? Email) : ICommand<CustomerDto>;

public sealed class CreateCustomerCommandHandler(ICustomerService service) : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public Task<CustomerDto> HandleAsync(CreateCustomerCommand cmd, CancellationToken ct = default)
        => service.CreateCustomerAsync(cmd.Name, cmd.Phone, cmd.Email);
}

public sealed class UpdateCustomerCommandHandler(ICustomerService service) : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public Task<CustomerDto> HandleAsync(UpdateCustomerCommand cmd, CancellationToken ct = default)
        => service.UpdateCustomerAsync(cmd.Id, cmd.Name, cmd.Phone, cmd.Email);
}

// ─── Settings Command ──────────────────────────────────────────────────────

public record SetSettingCommand(string Key, string Value, Guid UserId) : ICommand<OperationResult>;

public sealed class SetSettingCommandHandler(ISettingsService service) : ICommandHandler<SetSettingCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(SetSettingCommand cmd, CancellationToken ct = default)
        => service.SetSettingAsync(cmd.Key, cmd.Value, cmd.UserId);
}

// ─── Supplier Commands ─────────────────────────────────────────────────────

public record CreateSupplierCommand(string Name, string? ContactPerson, string? Phone, string? Email, string? Address) : ICommand<SupplierDto>;
public record UpdateSupplierCommand(Guid Id, string Name, string? ContactPerson, string? Phone, string? Email, string? Address) : ICommand<SupplierDto>;

public sealed class CreateSupplierCommandHandler(ISupplierService service) : ICommandHandler<CreateSupplierCommand, SupplierDto>
{
    public Task<SupplierDto> HandleAsync(CreateSupplierCommand cmd, CancellationToken ct = default)
        => service.CreateSupplierAsync(cmd.Name, cmd.ContactPerson, cmd.Phone, cmd.Email, cmd.Address);
}

public sealed class UpdateSupplierCommandHandler(ISupplierService service) : ICommandHandler<UpdateSupplierCommand, SupplierDto>
{
    public Task<SupplierDto> HandleAsync(UpdateSupplierCommand cmd, CancellationToken ct = default)
        => service.UpdateSupplierAsync(cmd.Id, cmd.Name, cmd.ContactPerson, cmd.Phone, cmd.Email, cmd.Address);
}

// ─── PurchaseOrder Commands ────────────────────────────────────────────────

public record CreatePurchaseOrderCommand(Guid SupplierId, Guid UserId, List<PurchaseOrderItemDto> Items, string? Notes) : ICommand<PurchaseOrderDto>;
public record ReceivePurchaseOrderCommand(Guid PurchaseOrderId, Guid UserId) : ICommand<OperationResult>;

public sealed class CreatePurchaseOrderCommandHandler(IPurchaseOrderService service) : ICommandHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    public Task<PurchaseOrderDto> HandleAsync(CreatePurchaseOrderCommand cmd, CancellationToken ct = default)
        => service.CreatePurchaseOrderAsync(cmd.SupplierId, cmd.UserId, cmd.Items, cmd.Notes);
}

public sealed class ReceivePurchaseOrderCommandHandler(IPurchaseOrderService service) : ICommandHandler<ReceivePurchaseOrderCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(ReceivePurchaseOrderCommand cmd, CancellationToken ct = default)
        => service.ReceivePurchaseOrderAsync(cmd.PurchaseOrderId, cmd.UserId);
}

// ─── Recipe Commands ───────────────────────────────────────────────────────

public record SaveRecipeCommand(Guid ProductId, string Name, string? Instructions, List<RecipeIngredientDto> Ingredients) : ICommand<RecipeDto>;

public sealed class SaveRecipeCommandHandler(IRecipeService service) : ICommandHandler<SaveRecipeCommand, RecipeDto>
{
    public Task<RecipeDto> HandleAsync(SaveRecipeCommand cmd, CancellationToken ct = default)
        => service.SaveRecipeAsync(cmd.ProductId, cmd.Name, cmd.Instructions, cmd.Ingredients);
}

// ─── Backup Commands ───────────────────────────────────────────────────────

public record CreateBackupCommand(Guid UserId) : ICommand<BackupDto>;
public record RestoreBackupCommand(Guid BackupId, Guid UserId) : ICommand<OperationResult>;
public record DeleteBackupCommand(Guid BackupId) : ICommand<OperationResult>;

public sealed class CreateBackupCommandHandler(IBackupManagementService service) : ICommandHandler<CreateBackupCommand, BackupDto>
{
    public Task<BackupDto> HandleAsync(CreateBackupCommand cmd, CancellationToken ct = default)
        => service.CreateBackupAsync(cmd.UserId);
}

public sealed class RestoreBackupCommandHandler(IBackupManagementService service) : ICommandHandler<RestoreBackupCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(RestoreBackupCommand cmd, CancellationToken ct = default)
        => service.RestoreBackupAsync(cmd.BackupId, cmd.UserId);
}

public sealed class DeleteBackupCommandHandler(IBackupManagementService service) : ICommandHandler<DeleteBackupCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(DeleteBackupCommand cmd, CancellationToken ct = default)
        => service.DeleteBackupAsync(cmd.BackupId);
}

// ─── Printer Commands ──────────────────────────────────────────────────────

public record AddPrinterCommand(string Name, string PrinterType, string Connection, string? IpAddress, string? Port, int PaperWidth, string Role) : ICommand<PrinterDto>;
public record TestPrinterCommand(Guid Id) : ICommand<bool>;
public record PrintReceiptCommand(Guid SaleId) : ICommand<bool>;

public sealed class AddPrinterCommandHandler(IPrinterManagementService service) : ICommandHandler<AddPrinterCommand, PrinterDto>
{
    public Task<PrinterDto> HandleAsync(AddPrinterCommand cmd, CancellationToken ct = default)
        => service.AddPrinterAsync(cmd.Name, cmd.PrinterType, cmd.Connection, cmd.IpAddress, cmd.Port, cmd.PaperWidth, cmd.Role);
}

public sealed class TestPrinterCommandHandler(IPrinterManagementService service) : ICommandHandler<TestPrinterCommand, bool>
{
    public Task<bool> HandleAsync(TestPrinterCommand cmd, CancellationToken ct = default)
        => service.TestPrinterAsync(cmd.Id);
}

public sealed class PrintReceiptCommandHandler(IPrinterManagementService service) : ICommandHandler<PrintReceiptCommand, bool>
{
    public Task<bool> HandleAsync(PrintReceiptCommand cmd, CancellationToken ct = default)
        => service.PrintReceiptAsync(cmd.SaleId);
}
