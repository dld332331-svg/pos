namespace POS.Domain.Interfaces;

/// <summary>
/// Interface for executing SQL Server backup/restore commands.
/// Enables unit testing of BackupService without a real SQL Server.
/// </summary>
public interface IDatabaseBackupExecutor
{
    /// <summary>
    /// Executes a BACKUP DATABASE command to the specified file path.
    /// </summary>
    Task BackupDatabaseAsync(string backupFilePath);

    /// <summary>
    /// Executes a RESTORE DATABASE command from the specified backup file.
    /// </summary>
    Task RestoreDatabaseAsync(string backupFilePath);

    /// <summary>
    /// Sets the database to SINGLE_USER mode, forcing other connections to disconnect.
    /// Required before RESTORE.
    /// </summary>
    Task SetSingleUserModeAsync();

    /// <summary>
    /// Restores the database to MULTI_USER mode after a restore operation.
    /// </summary>
    Task SetMultiUserModeAsync();
}
