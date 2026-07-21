namespace POS.Domain.Entities;

/// <summary>
/// Records a system backup operation.
/// This entity does NOT inherit BaseEntity as it has a fixed, non-modifiable schema.
/// </summary>
public sealed class BackupRecord
{
    /// <summary>Unique identifier for the backup record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>File system path where the backup file is stored.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Size of the backup file in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>ID of the user who initiated the backup.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>UTC timestamp when the backup was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the backup file has been verified for integrity.</summary>
    public bool IsVerified { get; set; }

    /// <summary>Number of times this backup has been used for a restore operation.</summary>
    public int RestoreCount { get; set; }
}