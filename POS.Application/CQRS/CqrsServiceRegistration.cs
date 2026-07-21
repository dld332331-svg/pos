using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS.Application.CQRS.Abstractions;
using POS.Application.CQRS.Commands;
using POS.Application.CQRS.Queries;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS;

/// <summary>
/// Registers the CQRS dispatcher and all command/query handlers in the DI container.
/// All handlers use TryAddScoped to avoid conflicts with existing registrations.
/// </summary>
public static class CqrsServiceRegistration
{
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        // Register dispatcher
        services.TryAddScoped<IDispatcher, Dispatcher>();

        // ── Auth ──────────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<LoginCommand, Domain.Interfaces.AuthResult>, LoginCommandHandler>();
        services.TryAddScoped<ICommandHandler<LogoutCommand>, LogoutCommandHandler>();

        // ── Products ──────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateProductCommand, ProductDto>, CreateProductCommandHandler>();
        services.TryAddScoped<ICommandHandler<UpdateProductCommand, ProductDto>, UpdateProductCommandHandler>();
        services.TryAddScoped<ICommandHandler<ArchiveProductCommand, OperationResult>, ArchiveProductCommandHandler>();
        services.TryAddScoped<ICommandHandler<CreateCategoryCommand, CategoryDto>, CreateCategoryCommandHandler>();

        services.TryAddScoped<IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>, GetProductsQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetProductByIdQuery, ProductDto?>, GetProductByIdQueryHandler>();
        services.TryAddScoped<IQueryHandler<FindByBarcodeQuery, ProductDto?>, FindByBarcodeQueryHandler>();
        services.TryAddScoped<IQueryHandler<FindBySkuQuery, ProductDto?>, FindBySkuQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetLowStockProductsQuery, List<ProductDto>>, GetLowStockProductsQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetCategoriesQuery, List<CategoryDto>>, GetCategoriesQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetModifierGroupsQuery, List<ModifierGroupDto>>, GetModifierGroupsQueryHandler>();

        // ── Sales ─────────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateNewSaleCommand, Guid>, CreateNewSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<AddItemToSaleCommand>, AddItemToSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<RemoveItemFromSaleCommand>, RemoveItemFromSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<UpdateSaleItemQuantityCommand>, UpdateSaleItemQuantityCommandHandler>();
        services.TryAddScoped<ICommandHandler<ModifySaleItemCommand, SaleItemDto>, ModifySaleItemCommandHandler>();
        services.TryAddScoped<ICommandHandler<ApplyDiscountCommand>, ApplyDiscountCommandHandler>();
        services.TryAddScoped<ICommandHandler<ProcessPaymentCommand, PaymentResult>, ProcessPaymentCommandHandler>();
        services.TryAddScoped<ICommandHandler<HoldSaleCommand, Guid>, HoldSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<RetrieveHeldSaleCommand, SaleSummaryDto>, RetrieveHeldSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<CancelSaleCommand, OperationResult>, CancelSaleCommandHandler>();
        services.TryAddScoped<ICommandHandler<ReturnItemsCommand, OperationResult>, ReturnItemsCommandHandler>();

        services.TryAddScoped<IQueryHandler<GetSaleSummaryQuery, SaleSummaryDto>, GetSaleSummaryQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetSaleItemsQuery, List<SaleItemDto>>, GetSaleItemsQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetHeldSalesQuery, List<HeldSaleDto>>, GetHeldSalesQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetSalesHistoryQuery, List<SaleSummaryDto>>, GetSalesHistoryQueryHandler>();

        // ── Inventory ─────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<AdjustStockCommand, OperationResult>, AdjustStockCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetInventoryStatusQuery, List<InventoryStatusDto>>, GetInventoryStatusQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetLowStockQuery, List<InventoryStatusDto>>, GetLowStockQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetInventoryMovementsQuery, PagedResult<InventoryMovementDto>>, GetInventoryMovementsQueryHandler>();

        // ── Shifts ────────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<OpenShiftCommand, ShiftDto>, OpenShiftCommandHandler>();
        services.TryAddScoped<ICommandHandler<CloseShiftCommand, ShiftDto>, CloseShiftCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetActiveShiftQuery, ShiftDto?>, GetActiveShiftQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetShiftHistoryQuery, List<ShiftDto>>, GetShiftHistoryQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetShiftSummaryQuery, ShiftSummaryDto>, GetShiftSummaryQueryHandler>();

        // ── Users ─────────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateUserCommand, UserDto>, CreateUserCommandHandler>();
        services.TryAddScoped<ICommandHandler<UpdateUserCommand, UserDto>, UpdateUserCommandHandler>();
        services.TryAddScoped<ICommandHandler<ToggleUserStatusCommand, OperationResult>, ToggleUserStatusCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetUsersQuery, List<UserDto>>, GetUsersQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetUserByIdQuery, UserDto?>, GetUserByIdQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetAllPermissionsQuery, List<string>>, GetAllPermissionsQueryHandler>();

        // ── Tables ────────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateTableCommand, TableDto>, CreateTableCommandHandler>();
        services.TryAddScoped<ICommandHandler<OpenTableCommand, OperationResult>, OpenTableCommandHandler>();
        services.TryAddScoped<ICommandHandler<CloseTableCommand, OperationResult>, CloseTableCommandHandler>();
        services.TryAddScoped<ICommandHandler<TransferOrderCommand, OperationResult>, TransferOrderCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetTablesQuery, List<TableDto>>, GetTablesQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetRoomsQuery, List<RoomDto>>, GetRoomsQueryHandler>();

        // ── Customers ─────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateCustomerCommand, CustomerDto>, CreateCustomerCommandHandler>();
        services.TryAddScoped<ICommandHandler<UpdateCustomerCommand, CustomerDto>, UpdateCustomerCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetCustomersQuery, List<CustomerDto>>, GetCustomersQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetCustomerOrderHistoryQuery, List<SaleSummaryDto>>, GetCustomerOrderHistoryQueryHandler>();

        // ── Settings ──────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<SetSettingCommand, OperationResult>, SetSettingCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetSettingQuery, string?>, GetSettingQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetSettingsByCategoryQuery, Dictionary<string, string>>, GetSettingsByCategoryQueryHandler>();

        // ── Dashboard ─────────────────────────────────────────────────────
        services.TryAddScoped<IQueryHandler<GetDashboardWidgetsQuery, List<DashboardWidgetDto>>, GetDashboardWidgetsQueryHandler>();

        // ── Suppliers ─────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateSupplierCommand, SupplierDto>, CreateSupplierCommandHandler>();
        services.TryAddScoped<ICommandHandler<UpdateSupplierCommand, SupplierDto>, UpdateSupplierCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetSuppliersQuery, List<SupplierDto>>, GetSuppliersQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetSupplierOrdersQuery, List<PurchaseOrderDto>>, GetSupplierOrdersQueryHandler>();

        // ── Purchase Orders ───────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>, CreatePurchaseOrderCommandHandler>();
        services.TryAddScoped<ICommandHandler<ReceivePurchaseOrderCommand, OperationResult>, ReceivePurchaseOrderCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetPurchaseOrdersQuery, List<PurchaseOrderDto>>, GetPurchaseOrdersQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>, GetPurchaseOrderByIdQueryHandler>();

        // ── Recipes ───────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<SaveRecipeCommand, RecipeDto>, SaveRecipeCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetRecipeByProductQuery, RecipeDto?>, GetRecipeByProductQueryHandler>();
        services.TryAddScoped<IQueryHandler<CalculateRecipeCostQuery, decimal>, CalculateRecipeCostQueryHandler>();

        // ── Printers ──────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<AddPrinterCommand, PrinterDto>, AddPrinterCommandHandler>();
        services.TryAddScoped<ICommandHandler<TestPrinterCommand, bool>, TestPrinterCommandHandler>();
        services.TryAddScoped<ICommandHandler<PrintReceiptCommand, bool>, PrintReceiptCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetPrintersQuery, List<PrinterDto>>, GetPrintersQueryHandler>();
        services.TryAddScoped<IQueryHandler<GetKitchenStationsQuery, List<KitchenStationDto>>, GetKitchenStationsQueryHandler>();

        // ── Audit ─────────────────────────────────────────────────────────
        services.TryAddScoped<IQueryHandler<GetAuditLogQuery, PagedResult<AuditLogDto>>, GetAuditLogQueryHandler>();

        // ── Backups ───────────────────────────────────────────────────────
        services.TryAddScoped<ICommandHandler<CreateBackupCommand, BackupDto>, CreateBackupCommandHandler>();
        services.TryAddScoped<ICommandHandler<RestoreBackupCommand, OperationResult>, RestoreBackupCommandHandler>();
        services.TryAddScoped<ICommandHandler<DeleteBackupCommand, OperationResult>, DeleteBackupCommandHandler>();
        services.TryAddScoped<IQueryHandler<GetBackupHistoryQuery, List<BackupDto>>, GetBackupHistoryQueryHandler>();

        return services;
    }
}
