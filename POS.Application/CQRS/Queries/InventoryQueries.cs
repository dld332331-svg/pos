using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

public record GetInventoryStatusQuery : IQuery<List<InventoryStatusDto>>;
public record GetLowStockQuery : IQuery<List<InventoryStatusDto>>;
public record GetInventoryMovementsQuery(Guid? ProductId, DateTime? From, DateTime? To, int Page = 1, int PageSize = 20) : IQuery<PagedResult<InventoryMovementDto>>;

public sealed class GetInventoryStatusQueryHandler(IInventoryService service) : IQueryHandler<GetInventoryStatusQuery, List<InventoryStatusDto>>
{
    public Task<List<InventoryStatusDto>> HandleAsync(GetInventoryStatusQuery q, CancellationToken ct = default)
        => service.GetCurrentStockAsync();
}

public sealed class GetLowStockQueryHandler(IInventoryService service) : IQueryHandler<GetLowStockQuery, List<InventoryStatusDto>>
{
    public Task<List<InventoryStatusDto>> HandleAsync(GetLowStockQuery q, CancellationToken ct = default)
        => service.GetLowStockAsync();
}

public sealed class GetInventoryMovementsQueryHandler(IInventoryService service) : IQueryHandler<GetInventoryMovementsQuery, PagedResult<InventoryMovementDto>>
{
    public Task<PagedResult<InventoryMovementDto>> HandleAsync(GetInventoryMovementsQuery q, CancellationToken ct = default)
        => service.GetMovementsAsync(q.ProductId, q.From, q.To, q.Page, q.PageSize);
}
