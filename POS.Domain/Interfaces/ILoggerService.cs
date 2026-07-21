namespace POS.Domain.Interfaces;

public interface ILoggerService : IDisposable
{
    void LogInfo(string message, params object?[] properties);
    void LogWarning(string message, params object?[] properties);
    void LogError(string message, Exception? exception = null, params object?[] properties);
    void LogDebug(string message, params object?[] properties);
    void LogError(Exception exception, string message);
}