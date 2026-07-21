namespace POS.Domain.Entities;

/// <summary>
/// Represents a system configuration setting (key-value pair with metadata).
/// </summary>
public class Setting : BaseEntity
{
    /// <summary>Unique key identifying the setting (e.g., "Tax.DefaultRate", "Receipt.Header").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The current value of the setting.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Category for grouping related settings (e.g., "Tax", "Receipt", "General").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Human-readable description of what this setting controls.</summary>
    public string? Description { get; set; }

    /// <summary>The factory default value for this setting, used for reset operations.</summary>
    public string? DefaultValue { get; set; }
}