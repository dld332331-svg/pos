using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

// ─── Command Records ───────────────────────────────────────────────────────

public record CreateNewSaleCommand(Guid UserId, Guid ShiftId, string? OrderType = null, Guid? TableId = null) : ICommand<Guid>;
public record AddItemToSaleCommand(Guid SaleId, AddItemRequest Request) : ICommand;
public record RemoveItemFromSaleCommand(Guid SaleId, Guid ItemId) : ICommand;
public record UpdateSaleItemQuantityCommand(Guid SaleId, Guid ItemId, decimal NewQuantity) : ICommand;
public record ModifySaleItemCommand(Guid SaleId, Guid ItemId, ModifierSelectionDto[] Modifiers) : ICommand<SaleItemDto>;
public record ApplyDiscountCommand(Guid SaleId, decimal DiscountAmount, string? Reason) : ICommand;
public record ProcessPaymentCommand(PaymentRequest Request) : ICommand<PaymentResult>;
public record HoldSaleCommand(Guid SaleId, string Reason) : ICommand<Guid>;
public record RetrieveHeldSaleCommand(Guid HeldSaleId) : ICommand<SaleSummaryDto>;
public record CancelSaleCommand(Guid SaleId, string Reason) : ICommand<OperationResult>;
public record ReturnItemsCommand(Guid OriginalSaleId, List<ReturnItemRequest> Items, string Reason) : ICommand<OperationResult>;

// ─── Handlers (delegate to existing services) ──────────────────────────────

public sealed class CreateNewSaleCommandHandler(ISaleService service) : ICommandHandler<CreateNewSaleCommand, Guid>
{
    public Task<Guid> HandleAsync(CreateNewSaleCommand cmd, CancellationToken ct = default)
        => service.CreateNewSaleAsync(cmd.UserId, cmd.ShiftId, cmd.OrderType, cmd.TableId);
}

public sealed class AddItemToSaleCommandHandler(ISaleService service) : ICommandHandler<AddItemToSaleCommand>
{
    public Task HandleAsync(AddItemToSaleCommand cmd, CancellationToken ct = default)
        => service.AddItemAsync(cmd.SaleId, cmd.Request);
}

public sealed class RemoveItemFromSaleCommandHandler(ISaleService service) : ICommandHandler<RemoveItemFromSaleCommand>
{
    public Task HandleAsync(RemoveItemFromSaleCommand cmd, CancellationToken ct = default)
        => service.RemoveItemAsync(cmd.SaleId, cmd.ItemId);
}

public sealed class UpdateSaleItemQuantityCommandHandler(ISaleService service) : ICommandHandler<UpdateSaleItemQuantityCommand>
{
    public Task HandleAsync(UpdateSaleItemQuantityCommand cmd, CancellationToken ct = default)
        => service.UpdateItemQuantityAsync(cmd.SaleId, cmd.ItemId, cmd.NewQuantity);
}

public sealed class ModifySaleItemCommandHandler(ISaleService service) : ICommandHandler<ModifySaleItemCommand, SaleItemDto>
{
    public Task<SaleItemDto> HandleAsync(ModifySaleItemCommand cmd, CancellationToken ct = default)
        => service.ModifyItemAsync(cmd.SaleId, cmd.ItemId, cmd.Modifiers);
}

public sealed class ApplyDiscountCommandHandler(ISaleService service) : ICommandHandler<ApplyDiscountCommand>
{
    public Task HandleAsync(ApplyDiscountCommand cmd, CancellationToken ct = default)
        => service.ApplyDiscountAsync(new ApplyDiscountRequest(cmd.SaleId, cmd.DiscountAmount, cmd.Reason));
}

public sealed class ProcessPaymentCommandHandler(ISaleService service) : ICommandHandler<ProcessPaymentCommand, PaymentResult>
{
    public Task<PaymentResult> HandleAsync(ProcessPaymentCommand cmd, CancellationToken ct = default)
        => service.ProcessPaymentAsync(cmd.Request);
}

public sealed class HoldSaleCommandHandler(ISaleService service) : ICommandHandler<HoldSaleCommand, Guid>
{
    public Task<Guid> HandleAsync(HoldSaleCommand cmd, CancellationToken ct = default)
        => service.HoldSaleAsync(cmd.SaleId, cmd.Reason);
}

public sealed class RetrieveHeldSaleCommandHandler(ISaleService service) : ICommandHandler<RetrieveHeldSaleCommand, SaleSummaryDto>
{
    public Task<SaleSummaryDto> HandleAsync(RetrieveHeldSaleCommand cmd, CancellationToken ct = default)
        => service.RetrieveHeldSaleAsync(cmd.HeldSaleId);
}

public sealed class CancelSaleCommandHandler(ISaleService service) : ICommandHandler<CancelSaleCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(CancelSaleCommand cmd, CancellationToken ct = default)
        => service.CancelSaleAsync(cmd.SaleId, cmd.Reason);
}

public sealed class ReturnItemsCommandHandler(ISaleService service) : ICommandHandler<ReturnItemsCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(ReturnItemsCommand cmd, CancellationToken ct = default)
        => service.ReturnItemsAsync(cmd.OriginalSaleId, cmd.Items, cmd.Reason);
}
