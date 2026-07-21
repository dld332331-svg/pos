using POS.Domain.Interfaces;

namespace POS.Benchmarks.Benchmarks;

/// <summary>
/// No-op logger for benchmark use — avoids I/O noise in measurements.
/// </summary>
public sealed class TestLogger : ILoggerService
{
    public void LogInfo(string message, params object?[] properties) { }
    public void LogWarning(string message, params object?[] properties) { }
    public void LogError(string message, Exception? exception = null, params object?[] properties) { }
    public void LogDebug(string message, params object?[] properties) { }
    public void LogError(Exception exception, string message) { }
    public void Dispose() { }
}
