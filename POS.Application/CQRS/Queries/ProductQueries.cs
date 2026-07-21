using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

// ─── Query Records ─────────────────────────────────────────────────────────

public record GetProductsQuery(ProductFilterDto Filter) : IQuery<PagedResult<ProductDto>>;
public record GetProductByIdQuery(Guid Id) : IQuery<ProductDto?>;
public record FindByBarcodeQuery(string Barcode) : IQuery<ProductDto?>;
public record FindBySkuQuery(string Sku) : IQuery<ProductDto?>;
public record GetLowStockProductsQuery : IQuery<List<ProductDto>>;
public record GetCategoriesQuery : IQuery<List<CategoryDto>>;
public record GetModifierGroupsQuery : IQuery<List<ModifierGroupDto>>;

// ─── Handlers (delegate to existing services) ──────────────────────────────

public sealed class GetProductsQueryHandler(IProductService service) : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    public Task<PagedResult<ProductDto>> HandleAsync(GetProductsQuery q, CancellationToken ct = default)
        => service.GetProductsAsync(q.Filter);
}

public sealed class GetProductByIdQueryHandler(IProductService service) : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public Task<ProductDto?> HandleAsync(GetProductByIdQuery q, CancellationToken ct = default)
        => service.GetProductByIdAsync(q.Id);
}

public sealed class FindByBarcodeQueryHandler(IProductService service) : IQueryHandler<FindByBarcodeQuery, ProductDto?>
{
    public Task<ProductDto?> HandleAsync(FindByBarcodeQuery q, CancellationToken ct = default)
        => service.FindByBarcodeAsync(q.Barcode);
}

public sealed class FindBySkuQueryHandler(IProductService service) : IQueryHandler<FindBySkuQuery, ProductDto?>
{
    public Task<ProductDto?> HandleAsync(FindBySkuQuery q, CancellationToken ct = default)
        => service.FindBySkuAsync(q.Sku);
}

public sealed class GetLowStockProductsQueryHandler(IProductService service) : IQueryHandler<GetLowStockProductsQuery, List<ProductDto>>
{
    public Task<List<ProductDto>> HandleAsync(GetLowStockProductsQuery q, CancellationToken ct = default)
        => service.GetLowStockProductsAsync();
}

public sealed class GetCategoriesQueryHandler(IProductService service) : IQueryHandler<GetCategoriesQuery, List<CategoryDto>>
{
    public Task<List<CategoryDto>> HandleAsync(GetCategoriesQuery q, CancellationToken ct = default)
        => service.GetCategoriesAsync();
}

public sealed class GetModifierGroupsQueryHandler(IProductService service) : IQueryHandler<GetModifierGroupsQuery, List<ModifierGroupDto>>
{
    public Task<List<ModifierGroupDto>> HandleAsync(GetModifierGroupsQuery q, CancellationToken ct = default)
        => service.GetModifierGroupsAsync();
}
