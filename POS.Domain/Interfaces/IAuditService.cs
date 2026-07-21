using POS.Domain.Enums;

namespace POS.Domain.Interfaces;

/// <summary>
/// Service for recording audit log entries.
/// All operations are asynchronous and fire-and-forget by design.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an audit log entry for the specified action.
    /// </summary>
    /// <param name="userId">ID of the user performing the action. Null for system actions.</param>
    /// <param name="actionType">The type of action being audited.</param>
    /// <param name="entityName">Name of the entity type affected (e.g., "Product", "Sale").</param>
    /// <param name="entityId">ID of the specific entity instance. Null if not applicable.</param>
    /// <param name="beforeValue">JSON-serialized state before the action. Null if not applicable.</param>
    /// <param name="afterValue">JSON-serialized state after the action. Null if not applicable.</param>
    /// <param name="reason">Human-readable reason for the action. Null if not applicable.</param>
    Task LogAsync(
        Guid? userId,
        AuditActionType actionType,
        string entityName,
        Guid? entityId,
        string? beforeValue,
        string? afterValue,
        string? reason);
}