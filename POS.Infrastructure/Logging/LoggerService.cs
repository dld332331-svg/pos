using Serilog;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Logging;

public class LoggerService : ILoggerService
{
    private readonly ILogger _logger;
    private bool _disposed;

    public LoggerService()
    {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: Path.Combine(logDirectory, "pos-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8)
            .CreateLogger();
    }

    public void LogInfo(string message, params object?[] properties)
    {
        _logger.Information(message, properties);
    }

    public void LogWarning(string message, params object?[] properties)
    {
        _logger.Warning(message, properties);
    }

    public void LogError(string message, Exception? exception = null, params object?[] properties)
    {
        if (exception != null)
            _logger.Error(exception, message, properties);
        else
            _logger.Error(message, properties);
    }

    public void LogDebug(string message, params object?[] properties)
    {
        _logger.Debug(message, properties);
    }

    public void LogError(Exception exception, string message)
    {
        _logger.Error(exception, message);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            (_logger as IDisposable)?.Dispose();
            _disposed = true;
        }
    }
}