using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Records an auditable event in the POS system.
/// This entity does NOT support soft delete or modification to preserve audit integrity.
/// Once written, an audit log entry must never be changed or deleted.
/// </summary>
public sealed class AuditLog
{
    /// <summary>Unique identifier for the audit log entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID of the user who performed the action. Null for system actions.</summary>
    public Guid? UserId { get; set; }

    /// <summary>UTC timestamp when the action occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>The type of action that was performed.</summary>
    public AuditActionType ActionType { get; set; }

    /// <summary>Name of the entity type affected (e.g., "Product", "Sale", "User").</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>ID of the specific entity instance that was affected.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>JSON-serialized representation of the entity state before the action (if applicable).</summary>
    public string? BeforeValue { get; set; }

    /// <summary>JSON-serialized representation of the entity state after the action (if applicable).</summary>
    public string? AfterValue { get; set; }

    /// <summary>Human-readable reason for the action (e.g., "Price correction", "Customer request").</summary>
    public string? Reason { get; set; }

    /// <summary>IP address of the client that initiated the action.</summary>
    public string? IPAddress { get; set; }
}