using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;

namespace POS.Infrastructure.Security;

/// <summary>
/// Audit logging service that records all auditable actions.
/// Audit records are immutable and non-deletable after creation.
/// </summary>
public class AuditLogger : IAuditService
{
    private readonly POSDbContext _context;
    private readonly ILoggerService _logger;

    public AuditLogger(POSDbContext context, ILoggerService logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the local IPv4 address via DNS. Override in tests to simulate DNS failures.
    /// Throws on failure — the caller (<see cref="GetLocalIpAddress"/>) handles the fallback.
    /// </summary>
    protected virtual string ResolveLocalIpAddressOrThrow()
    {
        return System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
            .AddressList
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?.ToString() ?? "127.0.0.1";
    }

    /// <summary>
    /// Resolves the local IPv4 address with a fallback to "127.0.0.1" on failure.
    /// The catch block logs a debug message. Override in integration tests if needed.
    /// </summary>
    protected virtual string GetLocalIpAddress()
    {
        try
        {
            return ResolveLocalIpAddressOrThrow();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not resolve local IP address for audit log: {Message}", ex.Message);
            return "127.0.0.1";
        }
    }

    public async Task LogAsync(
        Guid? userId,
        AuditActionType actionType,
        string entityName,
        Guid? entityId,
        string? beforeValue,
        string? afterValue,
        string? reason)
    {
        try
        {
            var ipAddress = GetLocalIpAddress();

            var entry = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                BeforeValue = beforeValue,
                AfterValue = afterValue,
                Reason = reason,
                IPAddress = ipAddress
            };

            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogDebug($"Audit: {actionType} on {entityName} {entityId} by user {userId}");
        }
        catch (Exception ex)
        {
            // Audit logging should never crash the application
            _logger.LogError($"Failed to write audit log: {ex.Message}");
        }
    }
}