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
            var ipAddress = "127.0.0.1";
            try
            {
                ipAddress = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.ToString() ?? "127.0.0.1";
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not resolve local IP address for audit log: {Message}", ex.Message);
            }

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