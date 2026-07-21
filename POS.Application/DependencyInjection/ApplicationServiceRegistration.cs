using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.CQRS;
using POS.Application.Services;
using POS.Application.Services.Implementations;

namespace POS.Application.DependencyInjection;

/// <summary>
/// Registers all Application-layer services and CQRS infrastructure.
/// Per spec §4.4 (Strict Dependency Direction), Application depends ONLY on Domain.
/// Infrastructure registration is the responsibility of the composition root
/// (POS.Desktop entry point), which must call AddInfrastructure() alongside this method.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Application Services ─────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPrinterManagementService, PrinterManagementService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IBackupManagementService, BackupManagementService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IKitchenOrderService, KitchenOrderService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IUnitConversionService, UnitConversionService>();

        // ── CQRS Infrastructure ──────────────────────────────────────────
        services.AddCqrs();

        return services;
    }
}
