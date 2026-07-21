using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Result of a backup operation.
/// </summary>
public class BackupResult
{
    /// <summary>Whether the backup was created successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The created backup record. Null if the backup failed.</summary>
    public BackupRecord? Record { get; set; }
}

/// <summary>
/// Result of a restore operation.
/// </summary>
public class RestoreResult
{
    /// <summary>Whether the restore was completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Service for creating and restoring database backups.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a new database backup.
    /// </summary>
    Task<BackupRecord> CreateBackupAsync(string? notes = null);

    /// <summary>
    /// Restores the database from a backup record.
    /// </summary>
    Task RestoreAsync(Guid backupRecordId, bool confirm = false);

    /// <summary>
    /// Gets the history of all backup records.
    /// </summary>
    Task<IReadOnlyList<BackupRecord>> GetBackupHistoryAsync();

    /// <summary>
    /// Deletes a backup record and its associated file.
    /// </summary>
    Task DeleteBackupAsync(Guid backupRecordId);
}