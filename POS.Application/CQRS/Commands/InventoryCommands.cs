using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

public record AdjustStockCommand(Guid ProductId, decimal Quantity, string AdjustmentType, string Reason, string? Notes, Guid UserId) : ICommand<OperationResult>;

public sealed class AdjustStockCommandHandler(IInventoryService service) : ICommandHandler<AdjustStockCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(AdjustStockCommand cmd, CancellationToken ct = default)
        => service.AdjustStockAsync(new StockAdjustmentRequest(cmd.ProductId, cmd.Quantity, cmd.Reason), cmd.UserId);
}
