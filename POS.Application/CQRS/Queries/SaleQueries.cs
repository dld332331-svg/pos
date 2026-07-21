using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

public record GetSaleSummaryQuery(Guid SaleId) : IQuery<SaleSummaryDto>;
public record GetSaleItemsQuery(Guid SaleId) : IQuery<List<SaleItemDto>>;
public record GetHeldSalesQuery(Guid ShiftId) : IQuery<List<HeldSaleDto>>;
public record GetSalesHistoryQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 20) : IQuery<List<SaleSummaryDto>>;

public sealed class GetSaleSummaryQueryHandler(ISaleService service) : IQueryHandler<GetSaleSummaryQuery, SaleSummaryDto>
{
    public Task<SaleSummaryDto> HandleAsync(GetSaleSummaryQuery q, CancellationToken ct = default)
        => service.GetSaleSummaryAsync(q.SaleId);
}

public sealed class GetSaleItemsQueryHandler(ISaleService service) : IQueryHandler<GetSaleItemsQuery, List<SaleItemDto>>
{
    public Task<List<SaleItemDto>> HandleAsync(GetSaleItemsQuery q, CancellationToken ct = default)
        => service.GetSaleItemsAsync(q.SaleId);
}

public sealed class GetHeldSalesQueryHandler(ISaleService service) : IQueryHandler<GetHeldSalesQuery, List<HeldSaleDto>>
{
    public Task<List<HeldSaleDto>> HandleAsync(GetHeldSalesQuery q, CancellationToken ct = default)
        => service.GetHeldSalesAsync(q.ShiftId);
}

public sealed class GetSalesHistoryQueryHandler(ISaleService service) : IQueryHandler<GetSalesHistoryQuery, List<SaleSummaryDto>>
{
    public Task<List<SaleSummaryDto>> HandleAsync(GetSalesHistoryQuery q, CancellationToken ct = default)
        => service.GetSalesHistoryAsync(q.From, q.To, q.Page, q.PageSize);
}
