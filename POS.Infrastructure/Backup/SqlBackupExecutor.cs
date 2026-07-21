using Microsoft.Data.SqlClient;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Backup;

/// <summary>
/// Executes SQL Server BACKUP DATABASE and RESTORE DATABASE commands via SqlConnection.
/// Requires the connecting user to have BACKUP DATABASE and RESTORE permissions on the target database.
/// </summary>
public class SqlBackupExecutor : IDatabaseBackupExecutor
{
    private readonly string _connectionString;
    private readonly ILoggerService? _logger;
    private string? _databaseName;

    public SqlBackupExecutor(string connectionString, ILoggerService? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Extracts the database name from the connection string.
    /// </summary>
    private string GetDatabaseName()
    {
        if (_databaseName != null)
            return _databaseName;

        var builder = new SqlConnectionStringBuilder(_connectionString);
        _databaseName = builder.InitialCatalog
            ?? throw new InvalidOperationException("Connection string does not specify an InitialCatalog (database name).");

        return _databaseName;
    }

    /// <summary>
    /// Executes a BACKUP DATABASE command to the specified file path.
    /// </summary>
    public async Task BackupDatabaseAsync(string backupFilePath)
    {
        var databaseName = GetDatabaseName();
        var sql = $@"
            BACKUP DATABASE [{databaseName}]
            TO DISK = @BackupPath
            WITH
                FORMAT,
                INIT,
                NAME = N'{databaseName}-Full Backup',
                COMPRESSION,
                STATS = 10,
                CHECKSUM";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 600; // 10 minutes
            command.Parameters.AddWithValue("@BackupPath", backupFilePath);

            await command.ExecuteNonQueryAsync();

            _logger?.LogInfo("SQL Server BACKUP DATABASE executed successfully: {Database} -> {Path}", databaseName, backupFilePath);
        }
        catch (SqlException ex)
        {
            _logger?.LogError($"SQL Server BACKUP DATABASE failed for {databaseName}: {ex.Message}", ex);
            throw new InvalidOperationException(
                $"Failed to backup database '{databaseName}'. Ensure the SQL Server account has BACKUP DATABASE permission. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a RESTORE DATABASE command from the specified backup file.
    /// </summary>
    public async Task RestoreDatabaseAsync(string backupFilePath)
    {
        var databaseName = GetDatabaseName();

        // First, get the logical file names from the backup
        var fileListSql = $@"
            RESTORE FILELISTONLY
            FROM DISK = @BackupPath";

        string logicalDataFile;
        string logicalLogFile;

        try
        {
            await using var listConnection = new SqlConnection(_connectionString);
            await listConnection.OpenAsync();

            await using var listCommand = new SqlCommand(fileListSql, listConnection);
            listCommand.CommandTimeout = 120;
            listCommand.Parameters.AddWithValue("@BackupPath", backupFilePath);

            await using var reader = await listCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Could not read backup file list. The backup file may be corrupt.");

            logicalDataFile = reader.GetString(reader.GetOrdinal("LogicalName"));

            await reader.NextResultAsync();
            if (await reader.ReadAsync())
            {
                logicalLogFile = reader.GetString(reader.GetOrdinal("LogicalName"));
            }
            else
            {
                logicalLogFile = logicalDataFile + "_log";
            }
        }
        catch (SqlException ex)
        {
            _logger?.LogError($"Failed to read backup file list: {ex.Message}", ex);
            throw new InvalidOperationException($"Failed to read backup file information from '{backupFilePath}'. Error: {ex.Message}", ex);
        }

        // Perform the actual restore
        var restoreSql = $@"
            RESTORE DATABASE [{databaseName}]
            FROM DISK = @BackupPath
            WITH
                REPLACE,
                RECOVERY,
                MOVE N'{logicalDataFile}' TO N'{GetDefaultDataPath()}',
                MOVE N'{logicalLogFile}' TO N'{GetDefaultLogPath()}',
                STATS = 10";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(restoreSql, connection);
            command.CommandTimeout = 600; // 10 minutes
            command.Parameters.AddWithValue("@BackupPath", backupFilePath);

            await command.ExecuteNonQueryAsync();

            _logger?.LogInfo("SQL Server RESTORE DATABASE executed successfully: {Database} <- {Path}", databaseName, backupFilePath);
        }
        catch (SqlException ex)
        {
            _logger?.LogError($"SQL Server RESTORE DATABASE failed for {databaseName}: {ex.Message}", ex);
            throw new InvalidOperationException(
                $"Failed to restore database '{databaseName}'. Ensure no other connections are active and the account has RESTORE permission. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the database to SINGLE_USER mode, forcing other connections to disconnect.
    /// Required before RESTORE.
    /// </summary>
    public async Task SetSingleUserModeAsync()
    {
        var databaseName = GetDatabaseName();
        var sql = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();

            _logger?.LogInfo("Database {Database} set to SINGLE_USER mode", databaseName);
        }
        catch (SqlException ex)
        {
            _logger?.LogError($"Failed to set SINGLE_USER mode: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Restores the database to MULTI_USER mode after a restore operation.
    /// </summary>
    public async Task SetMultiUserModeAsync()
    {
        var databaseName = GetDatabaseName();
        var sql = $"ALTER DATABASE [{databaseName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();

            _logger?.LogInfo("Database {Database} restored to MULTI_USER mode", databaseName);
        }
        catch (SqlException ex)
        {
            _logger?.LogError($"Failed to restore MULTI_USER mode: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the default SQL Server data file path for the current instance.
    /// </summary>
    private string GetDefaultDataPath()
    {
        var sql = "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataPath";
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        var result = command.ExecuteScalar()?.ToString() ?? "";
        var databaseName = GetDatabaseName();
        return Path.Combine(result, $"{databaseName}.mdf");
    }

    /// <summary>
    /// Gets the default SQL Server log file path for the current instance.
    /// </summary>
    private string GetDefaultLogPath()
    {
        var sql = "SELECT SERVERPROPERTY('InstanceDefaultLogPath') AS LogPath";
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        var result = command.ExecuteScalar()?.ToString() ?? "";
        var databaseName = GetDatabaseName();
        return Path.Combine(result, $"{databaseName}_log.ldf");
    }
}