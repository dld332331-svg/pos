using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Backup;

/// <summary>
/// Background service that performs automatic database backups on a configurable schedule.
/// Implements the document's automatic backup requirement (Section 33).
/// Controlled via appsettings.json → AppSettings:
///   "AutoBackupEnabled": bool (default false) — master switch for automatic backups.
///   "AutoBackupIntervalHours": int (default 24) — interval between backups, in hours.
/// </summary>
public class BackupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupBackgroundService> _logger;
    private readonly TimeSpan _backupInterval;
    private readonly bool _enabled;

    public BackupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BackupBackgroundService> logger,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);
        _serviceProvider = serviceProvider;
        _logger = logger;

        var section = configuration.GetSection("AppSettings");

        // Master switch (spec §33: automatic backup must be configurable)
        _enabled = bool.TryParse(section["AutoBackupEnabled"], out var enabled) && enabled;

        // Interval in hours, default 24, clamped to [1, 168] (1 hour .. 1 week)
        var hours = 24;
        if (int.TryParse(section["AutoBackupIntervalHours"], out var parsed))
            hours = Math.Clamp(parsed, 1, 168);
        _backupInterval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "BackupBackgroundService is disabled (AppSettings:AutoBackupEnabled = false). Automatic backups will not run.");
            return;
        }

        _logger.LogInformation(
            "BackupBackgroundService started. Interval: {Interval}", _backupInterval);

        // Perform initial backup after a short startup delay
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAutoBackupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic backup failed");
            }

            await Task.Delay(_backupInterval, stoppingToken);
        }
    }

    private async Task PerformAutoBackupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        _logger.LogInformation("Starting automatic backup at {Time}", DateTime.UtcNow);

        try
        {
            var record = await backupService.CreateBackupAsync("Automatic backup");
            _logger.LogInformation(
                "Automatic backup completed: {FilePath} ({Size} bytes, Verified: {Verified})",
                record.FilePath, record.FileSize, record.IsVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatic backup execution failed");
        }
    }
}
