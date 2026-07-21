using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

// ─── Table Queries ─────────────────────────────────────────────────────────

public record GetTablesQuery : IQuery<List<TableDto>>;
public record GetRoomsQuery : IQuery<List<RoomDto>>;

public sealed class GetTablesQueryHandler(ITableService service) : IQueryHandler<GetTablesQuery, List<TableDto>>
{
    public Task<List<TableDto>> HandleAsync(GetTablesQuery q, CancellationToken ct = default)
        => service.GetTablesAsync();
}

public sealed class GetRoomsQueryHandler(ITableService service) : IQueryHandler<GetRoomsQuery, List<RoomDto>>
{
    public Task<List<RoomDto>> HandleAsync(GetRoomsQuery q, CancellationToken ct = default)
        => service.GetRoomsAsync();
}

// ─── Customer Queries ──────────────────────────────────────────────────────

public record GetCustomersQuery(string? Search = null) : IQuery<List<CustomerDto>>;
public record GetCustomerOrderHistoryQuery(Guid CustomerId) : IQuery<List<SaleSummaryDto>>;

public sealed class GetCustomersQueryHandler(ICustomerService service) : IQueryHandler<GetCustomersQuery, List<CustomerDto>>
{
    public Task<List<CustomerDto>> HandleAsync(GetCustomersQuery q, CancellationToken ct = default)
        => service.GetCustomersAsync(q.Search);
}

public sealed class GetCustomerOrderHistoryQueryHandler(ICustomerService service) : IQueryHandler<GetCustomerOrderHistoryQuery, List<SaleSummaryDto>>
{
    public Task<List<SaleSummaryDto>> HandleAsync(GetCustomerOrderHistoryQuery q, CancellationToken ct = default)
        => service.GetCustomerOrderHistoryAsync(q.CustomerId);
}

// ─── Settings Queries ──────────────────────────────────────────────────────

public record GetSettingQuery(string Key) : IQuery<string?>;
public record GetSettingsByCategoryQuery(string Category) : IQuery<Dictionary<string, string>>;

public sealed class GetSettingQueryHandler(ISettingsService service) : IQueryHandler<GetSettingQuery, string?>
{
    public Task<string?> HandleAsync(GetSettingQuery q, CancellationToken ct = default)
        => service.GetSettingAsync(q.Key);
}

public sealed class GetSettingsByCategoryQueryHandler(ISettingsService service) : IQueryHandler<GetSettingsByCategoryQuery, Dictionary<string, string>>
{
    public Task<Dictionary<string, string>> HandleAsync(GetSettingsByCategoryQuery q, CancellationToken ct = default)
        => service.GetSettingsByCategoryAsync(q.Category);
}

// ─── Dashboard Queries ─────────────────────────────────────────────────────

public record GetDashboardWidgetsQuery(Guid UserId) : IQuery<List<DashboardWidgetDto>>;

public sealed class GetDashboardWidgetsQueryHandler(IDashboardService service) : IQueryHandler<GetDashboardWidgetsQuery, List<DashboardWidgetDto>>
{
    public Task<List<DashboardWidgetDto>> HandleAsync(GetDashboardWidgetsQuery q, CancellationToken ct = default)
        => service.GetWidgetsAsync(q.UserId);
}

// ─── Supplier Queries ──────────────────────────────────────────────────────

public record GetSuppliersQuery(string? Search = null) : IQuery<List<SupplierDto>>;
public record GetSupplierOrdersQuery(Guid SupplierId) : IQuery<List<PurchaseOrderDto>>;

public sealed class GetSuppliersQueryHandler(ISupplierService service) : IQueryHandler<GetSuppliersQuery, List<SupplierDto>>
{
    public Task<List<SupplierDto>> HandleAsync(GetSuppliersQuery q, CancellationToken ct = default)
        => service.GetSuppliersAsync(q.Search);
}

public sealed class GetSupplierOrdersQueryHandler(ISupplierService service) : IQueryHandler<GetSupplierOrdersQuery, List<PurchaseOrderDto>>
{
    public Task<List<PurchaseOrderDto>> HandleAsync(GetSupplierOrdersQuery q, CancellationToken ct = default)
        => service.GetSupplierOrdersAsync(q.SupplierId);
}

// ─── PurchaseOrder Queries ────────────────────────────────────────────────

public record GetPurchaseOrdersQuery(string? Status = null) : IQuery<List<PurchaseOrderDto>>;
public record GetPurchaseOrderByIdQuery(Guid Id) : IQuery<PurchaseOrderDto?>;

public sealed class GetPurchaseOrdersQueryHandler(IPurchaseOrderService service) : IQueryHandler<GetPurchaseOrdersQuery, List<PurchaseOrderDto>>
{
    public Task<List<PurchaseOrderDto>> HandleAsync(GetPurchaseOrdersQuery q, CancellationToken ct = default)
        => service.GetPurchaseOrdersAsync(q.Status);
}

public sealed class GetPurchaseOrderByIdQueryHandler(IPurchaseOrderService service) : IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>
{
    public Task<PurchaseOrderDto?> HandleAsync(GetPurchaseOrderByIdQuery q, CancellationToken ct = default)
        => service.GetPurchaseOrderAsync(q.Id);
}

// ─── Recipe Queries ────────────────────────────────────────────────────────

public record GetRecipeByProductQuery(Guid ProductId) : IQuery<RecipeDto?>;
public record CalculateRecipeCostQuery(Guid RecipeId) : IQuery<decimal>;

public sealed class GetRecipeByProductQueryHandler(IRecipeService service) : IQueryHandler<GetRecipeByProductQuery, RecipeDto?>
{
    public Task<RecipeDto?> HandleAsync(GetRecipeByProductQuery q, CancellationToken ct = default)
        => service.GetRecipeByProductAsync(q.ProductId);
}

public sealed class CalculateRecipeCostQueryHandler(IRecipeService service) : IQueryHandler<CalculateRecipeCostQuery, decimal>
{
    public Task<decimal> HandleAsync(CalculateRecipeCostQuery q, CancellationToken ct = default)
        => service.CalculateRecipeCostAsync(q.RecipeId);
}

// ─── Printer Queries ───────────────────────────────────────────────────────

public record GetPrintersQuery : IQuery<List<PrinterDto>>;
public record GetKitchenStationsQuery : IQuery<List<KitchenStationDto>>;

public sealed class GetPrintersQueryHandler(IPrinterManagementService service) : IQueryHandler<GetPrintersQuery, List<PrinterDto>>
{
    public Task<List<PrinterDto>> HandleAsync(GetPrintersQuery q, CancellationToken ct = default)
        => service.GetPrintersAsync();
}

public sealed class GetKitchenStationsQueryHandler(IPrinterManagementService service) : IQueryHandler<GetKitchenStationsQuery, List<KitchenStationDto>>
{
    public Task<List<KitchenStationDto>> HandleAsync(GetKitchenStationsQuery q, CancellationToken ct = default)
        => service.GetKitchenStationsAsync();
}

// ─── Audit Queries ─────────────────────────────────────────────────────────

public record GetAuditLogQuery(DateTime? From, DateTime? To, string? ActionType, string? EntityName, int Page = 1, int PageSize = 50) : IQuery<PagedResult<AuditLogDto>>;

public sealed class GetAuditLogQueryHandler(IAuditQueryService service) : IQueryHandler<GetAuditLogQuery, PagedResult<AuditLogDto>>
{
    public Task<PagedResult<AuditLogDto>> HandleAsync(GetAuditLogQuery q, CancellationToken ct = default)
        => service.GetAuditLogsAsync(q.From, q.To, q.ActionType, q.EntityName, q.Page, q.PageSize);
}

// ─── Backup Queries ────────────────────────────────────────────────────────

public record GetBackupHistoryQuery : IQuery<List<BackupDto>>;

public sealed class GetBackupHistoryQueryHandler(IBackupManagementService service) : IQueryHandler<GetBackupHistoryQuery, List<BackupDto>>
{
    public Task<List<BackupDto>> HandleAsync(GetBackupHistoryQuery q, CancellationToken ct = default)
        => service.GetBackupHistoryAsync();
}
