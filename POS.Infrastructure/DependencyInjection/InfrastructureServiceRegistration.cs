using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Interfaces;
using POS.Infrastructure.Backup;
using POS.Infrastructure.Database;
using POS.Infrastructure.Logging;
using POS.Infrastructure.Hardware;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Repositories;
using POS.Infrastructure.Security;

namespace POS.Infrastructure.DependencyInjection;

/// <summary>
/// Composition root for registering all Infrastructure-layer services.
/// This is the only place where Desktop should interact with Infrastructure.
/// </summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Registers all Infrastructure services (DbContext, Repositories, Security, Printing, Backup, Logging).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<POSDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")),
            ServiceLifetime.Scoped);

        // Register Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Security Services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuditService, AuditLogger>();
        services.AddScoped<IPermissionService, PermissionService>();

        // Register Printing Services
        services.AddScoped<IPrinterHardwareSender, RealPrinterHardwareSender>();
        services.AddScoped<IPrinterService, ESCPOSPrinter>();

        // Register Backup Services
        services.AddScoped<IDatabaseBackupExecutor>(sp =>
        {
            var connStr = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
            var logger = sp.GetRequiredService<ILoggerService>();
            return new SqlBackupExecutor(connStr, logger);
        });
        services.AddScoped<IBackupService, BackupService>();

        // Register Logging
        services.AddSingleton<ILoggerService, LoggerService>();

        // Register Background Services
        services.AddHostedService<BackupBackgroundService>();

        // Register Hardware Services
        services.AddSingleton<IBarcodeScannerService, BarcodeScannerService>();
        services.AddSingleton<ISoundService, SoundService>();

        return services;
    }
}
