using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog;
using POS.Desktop.Forms;
using POS.Desktop.Navigation;
using POS.Desktop.Services;
using POS.Domain.Interfaces;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.DependencyInjection;
using POS.Infrastructure.DependencyInjection;
using POS.Reporting.DependencyInjection;

namespace POS.Desktop;

static class Program
{
    /// <summary>
    /// Application entry point.
    /// Sets up DI container, registers all services via the Infrastructure Composition Root.
    /// Configures Serilog logging. Shows LoginForm on startup.
    /// On successful login, creates MainShell and navigates to Dashboard.
    /// Handles unhandled exceptions with Arabic error messages.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Initialize WinForms application settings
        ApplicationConfiguration.Initialize();

        // Configure unhandled exception modes
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // Global exception handlers
        System.Windows.Forms.Application.ThreadException += (sender, e) =>
        {
            LogException(e.Exception);
            ShowArabicError(
                "حدث خطأ غير متوقع",
                $"عذراً، حدث خطأ غير متوقع في النظام.\n\n" +
                $"التفاصيل: {e.Exception.Message}\n\n" +
                $"يرجى إعادة المحاولة أو التواصل مع الدعم الفني.",
                e.Exception
            );
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogException(ex);
            ShowArabicError(
                "خطأ حرج في النظام",
                $"حدث خطأ حرج لا يمكن التعافي منه.\n\n" +
                $"يرجى إعادة تشغيل البرنامج.",
                ex
            );
        };

        // Initialize embedded fonts (spec 7.3: Arabic fonts + 11.1: Font Awesome)
        FontLoader.Initialize();

        // Initialize DevExpress skins (spec 4.1)
        DevExpress.Skins.SkinManager.EnableFormSkins();
        try { DevExpress.UserSkins.BonusSkins.Register(); } catch (Exception ex) { System.Diagnostics.Trace.TraceWarning($"[Skins] BonusSkins not available, using default skin: {ex.Message}"); }
        DevExpress.LookAndFeel.UserLookAndFeel.Default.SkinName = "Office 2022 Colorful";

        // Build configuration
        var configuration = BuildConfiguration();

        // Build DI container
        var services = new ServiceCollection();

        // Register configuration
        services.AddSingleton<IConfiguration>(configuration);

        // === Register Application + Infrastructure Services ===
        // Composition Root (spec §4.4): the entry point wires Infrastructure into DI.
        // Application depends only on Domain; UI code never touches Infrastructure types.

        services.AddInfrastructure(configuration);
        services.AddApplicationServices(configuration);
        services.AddReportingServices();

// === Register Desktop Forms ===

        services.AddTransient<LoginForm>();
        services.AddTransient<DashboardForm>();
        services.AddTransient<PosTerminalForm>();
        services.AddTransient<ProductListForm>();
        services.AddTransient<ProductForm>();
        services.AddTransient<InventoryForm>();
        services.AddTransient<StockAdjustmentDialog>();
        services.AddTransient<ReportForm>();
        services.AddTransient<UserManagementForm>();
        services.AddTransient<SettingsForm>();
        services.AddTransient<BackupForm>();
        services.AddTransient<AuditLogForm>();
        services.AddTransient<PrinterManagementForm>();
        services.AddTransient<CustomerListForm>();
        services.AddTransient<KitchenDisplayForm>();
        services.AddTransient<TableMapForm>();
        services.AddTransient<PaymentDialog>();
        services.AddTransient<ShiftForm>();
        services.AddTransient<SupplierListForm>();
        services.AddTransient<PurchaseOrderForm>();
        services.AddTransient<PromotionsListForm>();
        services.AddTransient<ReturnForm>();
        services.AddTransient<ExpenseDialog>();
        services.AddTransient<HoldSaleDialog>();
        services.AddTransient<WithdrawalDepositDialog>();

        // === Register Navigation Shell ===

        services.AddTransient<MainShell>();

        // === Register Desktop Services ===

        services.AddSingleton<INotificationService, NotificationService>();

        // === Register Serilog Logger (spec 4.1 — structured logging) ===
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.File(
                path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "pos-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8
            )
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Store provider for global access
        AppServiceProvider.Provider = serviceProvider;

        // === Initialize database (spec §3.1: login must work on-premises; §44: repeatable deployment) ===
        // Applies pending migrations and seeds required data (admin user, permissions, settings).
        // On failure the error is logged and the LoginForm will show the DatabaseUnavailable state.
        try
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Database.POSDbContext>();
                var seedPassword = configuration.GetSection("AppSettings")["SeedAdminPassword"];
                POS.Infrastructure.Database.DbInitializer
                    .SeedData(dbContext, seedPassword)
                    .GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            LogException(ex);
        }

        // ====== Run application ======
        // Login form is shown as a MODAL DIALOG before the main message pump
        // starts. On success, MainShell is shown via Application.Run (proper
        // top-level message pump). When the user logs out, the while-loop
        // re-shows the login dialog without restarting the process.
        //
        // This avoids the orphaned-modal-pump problem that previously caused
        // blank windows, missing taskbar entries, and uncloseable windows.
        bool shouldRestart = false;

        while (true)
        {
            LoginResponse? loginResponse = null;

            try
            {
                // ── Show login dialog (modal, before main pump) ──
                loginResponse = ShowLoginDialog(serviceProvider);
                if (loginResponse == null)
                    break;

                // ── Create MainShell ──
                var mainShell = serviceProvider.GetRequiredService<MainShell>();

                // Wire navigation events
                mainShell.OnNavigateToDashboard += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<DashboardForm>());
                mainShell.OnNavigateToPOS += (s, e) =>
                    mainShell.NavigateToPOS(serviceProvider);
                mainShell.OnNavigateToProducts += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<ProductListForm>());
                mainShell.OnNavigateToInventory += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<InventoryForm>());
                mainShell.OnNavigateToReports += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<ReportForm>());
                mainShell.OnNavigateToUsers += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<UserManagementForm>());
                mainShell.OnNavigateToSettings += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<SettingsForm>());
                mainShell.OnNavigateToPrinters += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<PrinterManagementForm>());
                mainShell.OnNavigateToAudit += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<AuditLogForm>());
                mainShell.OnNavigateToBackup += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<BackupForm>());
                mainShell.OnNavigateToPromotions += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<PromotionsListForm>());
                mainShell.OnNavigateToTables += (s, e) =>
                    mainShell.NavigateTo(serviceProvider.GetRequiredService<TableMapForm>());
                mainShell.OnNavigateToReturns += (s, e) =>
                {
                    var returnForm = serviceProvider.GetRequiredService<ReturnForm>();
                    mainShell.NavigateTo(returnForm);
                };

                mainShell.OnLogout += (s, e) =>
                {
                    shouldRestart = true;
                    mainShell.Close();
                };

                mainShell.OnLock += (s, e) =>
                {
                    var lockForm = serviceProvider.GetRequiredService<LoginForm>();
                    lockForm.ShowDialog(mainShell);
                    RefreshShiftInfo(mainShell, serviceProvider, AppServiceProvider.CurrentUserId);
                };

                // Set user context
                AppServiceProvider.CurrentUserId = loginResponse.UserId;
                AppServiceProvider.CurrentUserRole = loginResponse.Role;
                AppServiceProvider.CurrentUserDisplayName = loginResponse.DisplayName;
                var permissions = loginResponse.Permissions ?? new List<string>();
                mainShell.SetUserContext(loginResponse.UserId, loginResponse.DisplayName, loginResponse.Role, permissions);
                RefreshShiftInfo(mainShell, serviceProvider, loginResponse.UserId);

                // Load the dashboard as the initial content.
                mainShell.NavigateTo(serviceProvider.GetRequiredService<DashboardForm>());

                // ── Run MainShell as the top-level application window ──
                // Application.Run creates the proper main message pump, ensuring:
                //   - The window appears in the taskbar
                //   - Close/minimize/restore work normally
                //   - All child controls paint correctly
                //   - DevExpress XtraForm skinning works correctly
                System.Windows.Forms.Application.Run(mainShell);
            }
            catch (Exception ex)
            {
                LogException(ex);
                ShowArabicError(
                    "خطأ في تشغيل البرنامج",
                    $"حدث خطأ غير متوقع. يرجى إعادة تشغيل البرنامج.",
                    ex
                );
                break;
            }

            if (!shouldRestart) break;

            // Logout requested — loop back to login screen
            shouldRestart = false;
        }

        // Cleanup
        Log.CloseAndFlush();
        (serviceProvider as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Shows the login form as a modal dialog. Returns the LoginResponse on
    /// successful authentication, or null if the user cancelled or login failed.
    /// </summary>
    internal static LoginResponse? ShowLoginDialog(IServiceProvider serviceProvider)
    {
        using var loginForm = serviceProvider.GetRequiredService<LoginForm>();
        LoginResponse? response = null;

        loginForm.LoginSuccessful += (sender, args) =>
        {
            response = args;
            loginForm.DialogResult = DialogResult.OK;
            loginForm.Close();
        };

        var result = loginForm.ShowDialog();
        return result == DialogResult.OK ? response : null;
    }

    /// <summary>
    /// Loads the current open shift for the user and updates the shell label.
    /// Falls back to a static message if no shift is open or the service is unavailable.
    /// </summary>
    static void RefreshShiftInfo(MainShell shell, IServiceProvider serviceProvider, Guid userId)
    {
        try
        {
            var shiftService = serviceProvider.GetService<IShiftService>();
            if (shiftService == null)
            {
                shell.SetShiftInfo("الوردية: غير متاحة");
                return;
            }

            var shift = shiftService.GetCurrentShiftAsync(userId).GetAwaiter().GetResult();
            if (shift != null)
            {
                shell.SetShiftInfo($"الوردية: #{shift.ShiftNumber}");
            }
            else
            {
                shell.SetShiftInfo("الوردية: مغلقة");
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Could not load current shift: {Message}", ex.Message);
            shell.SetShiftInfo("الوردية: غير معروفة");
        }
    }

    /// <summary>
    /// Builds the application configuration from app.config / appsettings.json.
    /// </summary>
    static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Logs an exception to the logging system via Serilog.
    /// </summary>
    static void LogException(Exception? ex)
    {
        Log.Error(ex, "Unhandled exception occurred");
        System.Diagnostics.Trace.TraceError($"[POS Error] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {ex?.Message ?? "Unknown error"}\n{ex?.StackTrace ?? ""}");
    }

    /// <summary>
    /// Shows an Arabic error dialog to the user.
    /// </summary>
    static void ShowArabicError(string title, string message, Exception? ex = null)
    {
        try
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
            );
        }
        catch
        {
            // Fallback if even MessageBox fails
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// Static accessor for the application's DI service provider.
/// </summary>
public static class AppServiceProvider
{
    public static IServiceProvider? Provider { get; set; }
    public static Guid CurrentUserId { get; set; }
    public static string CurrentUserRole { get; set; } = "";
    public static string CurrentUserDisplayName { get; set; } = "";
}

/*
 * === app.config content ===
 * 
 * <?xml version="1.0" encoding="utf-8"?>
 * <configuration>
 *   <configSections>
 *     <section name="entityFramework" type="System.Data.Entity.Internal.ConfigFile.EntityFrameworkSection, EntityFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" requirePermission="false" />
 *   </configSections>
 *   <connectionStrings>
 *     <add name="DefaultConnection"
 *          connectionString="Server=localhost;Database=POS_DB;Trusted_Connection=True;TrustServerCertificate=True;"
 *          providerName="System.Data.SqlClient" />
 *   </connectionStrings>
 *   <appSettings>
 *     <add key="BackupPath" value="C:\POS_Backups" />
 *     <add key="AutoBackupEnabled" value="true" />
 *     <add key="AutoBackupIntervalHours" value="24" />
 *     <add key="MaxLoginAttempts" value="5" />
 *     <add key="SessionTimeout" value="30" />
 *     <add key="ReceiptPrinter" value="ReceiptPrinter001" />
 *     <add key="KitchenPrinter" value="KitchenPrinter001" />
 *     <add key="CurrencySymbol" value="JOD" />
 *     <add key="CurrencyDecimals" value="3" />
 *     <add key="DefaultTaxRate" value="16" />
 *     <add key="TaxInclusive" value="false" />
 *     <add key="SoundsEnabled" value="true" />
 *     <add key="Volume" value="70" />
 *   </appSettings>
 *   <entityFramework>
 *     <defaultConnectionFactory type="System.Data.Entity.Infrastructure.LocalDbConnectionFactory, EntityFramework" />
 *     <providers>
 *       <provider invariantName="System.Data.SqlClient" type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer" />
 *     </providers>
 *   </entityFramework>
 *   <startup>
 *     <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
 *   </startup>
 * </configuration>
 * 
 * 
 * === appsettings.json content ===
 * 
 * {
 *   "ConnectionStrings": {
 *     "DefaultConnection": "Server=localhost;Database=POS_DB;Trusted_Connection=True;TrustServerCertificate=True;"
 *   },
 *   "Serilog": {
 *     "MinimumLevel": {
 *       "Default": "Information",
 *       "Override": {
 *         "Microsoft": "Warning",
 *         "System": "Warning"
 *       }
 *     },
 *     "WriteTo": [
 *       {
 *         "Name": "File",
 *         "Args": {
 *           "path": "logs/pos-.log",
 *           "rollingInterval": "Day",
 *           "retainedFileCountLimit": 30,
 *           "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {Message:lj}{NewLine}{Exception}",
 *           "encoding": "UTF-8"
 *         }
 *       },
 *       {
 *         "Name": "Console"
 *       }
 *     ]
 *   },
 *   "AppSettings": {
 *     "BackupPath": "C:\\POS_Backups",
 *     "AutoBackupEnabled": false,
 *     "AutoBackupIntervalHours": 24,
 *     "MaxLoginAttempts": 5,
 *     "SessionTimeoutMinutes": 30,
 *     "CurrencySymbol": "JOD",
 *     "CurrencyDecimals": 3,
 *     "DefaultTaxRate": 16.0,
 *     "TaxInclusive": false,
 *     "SoundsEnabled": true,
 *     "Volume": 70
 *   }
 * }
 */