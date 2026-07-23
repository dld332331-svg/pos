using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Backup;

using Microsoft.Data.SqlClient;

public class BackupService : IBackupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseBackupExecutor _backupExecutor;
    private readonly ILoggerService _logger;
    private readonly string _connectionString;

    private const string BackupDirectory = "backups";
    
    /// <summary>
    /// Maximum number of backup files to retain. Older backups are auto-deleted.
    /// </summary>
    private const int MaxBackupRetentionCount = 30;
    
    /// <summary>
    /// Maximum age of backup files in days. Backups older than this are auto-deleted.
    /// </summary>
    private const int MaxBackupRetentionDays = 90;

    public BackupService(IUnitOfWork unitOfWork, IDatabaseBackupExecutor backupExecutor, ILoggerService logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _unitOfWork = unitOfWork;
        _backupExecutor = backupExecutor;
        _logger = logger;
        _connectionString = configuration.GetSection("ConnectionStrings")["DefaultConnection"]
            ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
    }

    /// <summary>
    /// Enforces the retention policy by deleting backups that exceed retention limits.
    /// </summary>
    private async Task EnforceRetentionPolicyAsync()
    {
        try
        {
            var records = (await _unitOfWork.BackupRecords.GetAllAsync())
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            var toDelete = new List<BackupRecord>();

            // Delete by count: keep only the newest MaxBackupRetentionCount
            if (records.Count > MaxBackupRetentionCount)
            {
                toDelete.AddRange(records.Skip(MaxBackupRetentionCount));
            }

            // Delete by age: remove backups older than MaxBackupRetentionDays
            var cutoff = DateTime.UtcNow.AddDays(-MaxBackupRetentionDays);
            var oldRecords = records.Where(r => r.CreatedAt < cutoff).ToList();
            toDelete = toDelete.Union(oldRecords).Distinct().ToList();

            foreach (var record in toDelete)
            {
                if (File.Exists(record.FilePath))
                {
                    try { File.Delete(record.FilePath); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not delete old backup file {FilePath}: {Error}", record.FilePath, ex.Message);
                    }
                }

                // Note: DB records are kept for audit purposes even after file deletion
            }

            if (toDelete.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInfo("Retention policy applied: {Count} old backup(s) deleted", toDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to enforce backup retention policy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifies backup integrity using RESTORE VERIFYONLY.
    /// </summary>
    private async Task<bool> VerifyBackupAsync(string backupFilePath)
    {
        try
        {
            var sql = $"RESTORE VERIFYONLY FROM DISK = @BackupPath";
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 120;
            command.Parameters.AddWithValue("@BackupPath", backupFilePath);
            await command.ExecuteNonQueryAsync();
            _logger.LogInfo("Backup integrity verified successfully: {Path}", backupFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Backup integrity verification failed: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<BackupRecord> CreateBackupAsync(string? notes = null)
    {
        Directory.CreateDirectory(BackupDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"POS_Backup_{timestamp}.bak";
        var backupFilePath = Path.Combine(BackupDirectory, backupFileName);

        try
        {
            _logger.LogInfo("Starting database backup to {FilePath}", backupFilePath);

            // Execute SQL Server backup
            await _backupExecutor.BackupDatabaseAsync(backupFilePath);

            // Verify backup integrity using RESTORE VERIFYONLY
            var isVerified = await VerifyBackupAsync(backupFilePath);

            // Get file size
            var fileInfo = new FileInfo(backupFilePath);
            var fileSizeInBytes = fileInfo.Exists ? fileInfo.Length : 0;

            // Create backup record
            var record = new BackupRecord
            {
                Id = Guid.NewGuid(),
                FilePath = backupFilePath,
                FileSize = fileSizeInBytes,
                CreatedAt = DateTime.UtcNow,
                IsVerified = isVerified
            };

            await _unitOfWork.BackupRecords.AddAsync(record);
            await _unitOfWork.SaveChangesAsync();

            // Enforce retention policy after successful backup
            await EnforceRetentionPolicyAsync();

            _logger.LogInfo("Database backup completed: {FilePath} ({FileSize} bytes) — Verified: {IsVerified}", backupFilePath, fileSizeInBytes, isVerified);
            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Database backup failed: {ex.Message}", ex);

            var failedRecord = new BackupRecord
            {
                Id = Guid.NewGuid(),
                FilePath = backupFilePath,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _unitOfWork.BackupRecords.AddAsync(failedRecord);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception recordEx)
            {
                _logger.LogError(recordEx, "Failed to record failed backup attempt");
            }

            throw;
        }
    }

    public async Task RestoreAsync(Guid backupRecordId, bool confirm = false)
    {
        if (!confirm)
        {
            throw new InvalidOperationException(
                "Restoration must be explicitly confirmed by setting confirm = true.");
        }

        var backupRecord = await _unitOfWork.BackupRecords.GetByIdAsync(backupRecordId);

        if (backupRecord == null)
            throw new FileNotFoundException($"Backup record with ID '{backupRecordId}' not found.");

        if (!File.Exists(backupRecord.FilePath))
            throw new FileNotFoundException($"Backup file not found at path: {backupRecord.FilePath}");

        _logger.LogWarning("Starting database restore from {FilePath} (ID: {Id})", backupRecord.FilePath, backupRecord.Id);

        try
        {
            await _backupExecutor.SetSingleUserModeAsync();

            try
            {
                await _backupExecutor.RestoreDatabaseAsync(backupRecord.FilePath);
                _logger.LogInfo("Database restore completed from {FilePath}", backupRecord.FilePath);
            }
            finally
            {
                await _backupExecutor.SetMultiUserModeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Database restore failed: {ex.Message}", ex);
            await _backupExecutor.SetMultiUserModeAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<BackupRecord>> GetBackupHistoryAsync()
    {
        var records = await _unitOfWork.BackupRecords.GetAllAsync();
        return records.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public async Task DeleteBackupAsync(Guid backupRecordId)
    {
        var records = await _unitOfWork.BackupRecords.GetAllAsync();
        var record = records.FirstOrDefault(b => b.Id == backupRecordId);
        if (record == null)
            throw new FileNotFoundException($"Backup record with ID '{backupRecordId}' not found.");

        if (File.Exists(record.FilePath))
        {
            try
            {
                File.Delete(record.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not delete backup file {FilePath}: {Error}", record.FilePath, ex.Message);
            }
        }

        _logger.LogInfo("Backup record {Id} deleted", backupRecordId);
    }
}