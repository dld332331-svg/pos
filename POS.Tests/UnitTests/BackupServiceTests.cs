#nullable enable

using Xunit;
using Moq;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure.Backup;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for BackupService covering all 4 public methods:
/// CreateBackupAsync, RestoreAsync, GetBackupHistoryAsync, DeleteBackupAsync.
/// Uses mocked IDatabaseBackupExecutor to avoid real SQL Server connections.
/// Uses real filesystem for FileInfo/File.Exists/File.Delete interactions.
/// </summary>
public class BackupServiceTests : IDisposable
{
    // ========================================================================
    // Test Setup & Teardown
    // ========================================================================

    /// <summary>
    /// Cleans up the backups directory after each test.
    /// </summary>
    public void Dispose()
    {
        const string backupDir = "backups";
        if (Directory.Exists(backupDir))
        {
            try
            {
                Directory.Delete(backupDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        GC.SuppressFinalize(this);
    }

    // ========================================================================
    // Mock Builders
    // ========================================================================

    /// <summary>
    /// Creates a test backup record with the given properties.
    /// </summary>
    private static BackupRecord CreateTestRecord(
        Guid? id = null,
        string filePath = @"backups\POS_Backup_20260719_120000.bak",
        long fileSize = 1024,
        DateTime? createdAt = null)
    {
        return new BackupRecord
        {
            Id = id ?? Guid.NewGuid(),
            FilePath = filePath,
            FileSize = fileSize,
            CreatedAt = createdAt ?? new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Builds a BackupService with fully mocked dependencies.
    /// </summary>
    private (BackupService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IDatabaseBackupExecutor> executorMock, Mock<ILoggerService> loggerMock)
        BuildServiceWithMocks(
            List<BackupRecord>? backupRecords = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var executorMock = new Mock<IDatabaseBackupExecutor>();
        var loggerMock = new Mock<ILoggerService>();

        // Mock IConfiguration to return a connection string via indexer
        var configSectionMock = new Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
        configSectionMock.Setup(s => s["DefaultConnection"]).Returns("Server=localhost;Database=POS_DB_Test;Trusted_Connection=True;TrustServerCertificate=True;");
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c.GetSection("ConnectionStrings")).Returns(configSectionMock.Object);

        // Default logger setup — all methods return void, no special setup needed
        loggerMock.Setup(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object?[]>()));
        loggerMock.Setup(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object?[]>()));
        loggerMock.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<object?[]>()));
        loggerMock.Setup(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()));

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- BackupRecords repository (ISimpleRepository) ----
        var backupRepoMock = new Mock<ISimpleRepository<BackupRecord>>();
        backupRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(backupRecords ?? new List<BackupRecord>());
        backupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => backupRecords?.FirstOrDefault(b => b.Id == id));
        backupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<BackupRecord>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.BackupRecords).Returns(backupRepoMock.Object);

        var service = new BackupService(unitOfWorkMock.Object, executorMock.Object, loggerMock.Object, configMock.Object);
        return (service, unitOfWorkMock, executorMock, loggerMock);
    }

    // ========================================================================
    // CreateBackupAsync — Backup Creation
    // ========================================================================

    [Fact]
    public async Task CreateBackupAsync_HappyPath_CreatesBackupFileAndRecord()
    {
        // Arrange
        var (service, unitOfWorkMock, executorMock, loggerMock) = BuildServiceWithMocks();

        // When executor.BackupDatabaseAsync is called, create the actual file
        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, "test backup content");
            })
            .Returns(Task.CompletedTask);

        // Setup VerifyBackupAsync to return true (backup is valid)
        executorMock
            .Setup(e => e.VerifyBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.CreateBackupAsync();

        // Assert — record was created with correct properties
        result.Should().NotBeNull();
        result.FilePath.Should().Contain("POS_Backup_");
        result.FilePath.Should().EndWith(".bak");
        result.FileSize.Should().BeGreaterThan(0);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // BackupDatabaseAsync was called
        executorMock.Verify(e => e.BackupDatabaseAsync(It.IsAny<string>()), Times.Once);
        executorMock.Verify(e => e.VerifyBackupAsync(It.IsAny<string>()), Times.Once);

        // Backup was verified
        result.IsVerified.Should().BeTrue();

        // Backup record was saved
        unitOfWorkMock.Verify(u => u.BackupRecords.AddAsync(It.IsAny<BackupRecord>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        // Info log messages were written (start + completion)
        loggerMock.Verify(l => l.LogInfo("Starting database backup to {FilePath}", It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateBackupAsync_ExecutorFails_CreatesFailedRecordAndRethrows()
    {
        // Arrange
        var (service, unitOfWorkMock, executorMock, loggerMock) = BuildServiceWithMocks();

        var expectedException = new InvalidOperationException("Backup failed");
        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = () => service.CreateBackupAsync();

        // Assert — original exception is rethrown
        var ex = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Backup failed");

        // Failed record was saved
        unitOfWorkMock.Verify(u => u.BackupRecords.AddAsync(It.IsAny<BackupRecord>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        // Error was logged
        loggerMock.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Backup")),
            expectedException,
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateBackupAsync_FailedRecordSaveFails_ErrorSwallowedStillRethrows()
    {
        // Arrange — saving the failed record also throws
        var (service, unitOfWorkMock, executorMock, _) = BuildServiceWithMocks();

        // Make saving ANY BackupRecord throw
        var backupRepoMock = Mock.Get(unitOfWorkMock.Object.BackupRecords);
        backupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<BackupRecord>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var expectedException = new InvalidOperationException("Executor failure");
        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = () => service.CreateBackupAsync();

        // Assert — original executor exception is rethrown (failed record save is swallowed)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Executor failure");

        // Failed record add was attempted (and failed silently)
        backupRepoMock.Verify(r => r.AddAsync(It.IsAny<BackupRecord>()), Times.Once);
    }

    [Fact]
    public async Task CreateBackupAsync_VerifyFails_IsVerifiedIsFalse()
    {
        // Arrange — executor.BackupDatabaseAsync succeeds, VerifyBackupAsync returns false
        var (service, unitOfWorkMock, executorMock, _) = BuildServiceWithMocks();

        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        executorMock
            .Setup(e => e.VerifyBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.CreateBackupAsync();

        // Assert — IsVerified is false when verification fails
        result.IsVerified.Should().BeFalse();
        executorMock.Verify(e => e.VerifyBackupAsync(It.IsAny<string>()), Times.Once);
        unitOfWorkMock.Verify(u => u.BackupRecords.AddAsync(It.IsAny<BackupRecord>()), Times.Once);
    }

    [Fact]
    public async Task CreateBackupAsync_FileDoesNotExist_SizeIsZero()
    {
        // Arrange — executor succeeds but no file is actually created
        var (service, unitOfWorkMock, executorMock, _) = BuildServiceWithMocks();

        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.CreateBackupAsync();

        // Assert — file size is 0 since no backup file was actually written
        result.FileSize.Should().Be(0);
        result.IsVerified.Should().BeFalse(); // default mock returns false

        // Record was still saved
        unitOfWorkMock.Verify(u => u.BackupRecords.AddAsync(It.IsAny<BackupRecord>()), Times.Once);
    }

    // ========================================================================
    // RestoreAsync — Restore from Backup
    // ========================================================================

    [Fact]
    public async Task RestoreAsync_NotConfirmed_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.RestoreAsync(Guid.NewGuid(), confirm: false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Restoration must be explicitly confirmed by setting confirm = true.");
    }

    [Fact]
    public async Task RestoreAsync_RecordNotFound_ThrowsFileNotFoundException()
    {
        // Arrange — no backup records in repo
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.RestoreAsync(Guid.NewGuid(), confirm: true);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RestoreAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange — record exists but file path doesn't point to an actual file
        var record = CreateTestRecord(filePath: @"backups\nonexistent.bak");
        var (service, _, _, _) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { record });

        // Act
        var act = () => service.RestoreAsync(record.Id, confirm: true);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*not found at path*");
    }

    [Fact]
    public async Task RestoreAsync_HappyPath_SetsSingleUserRestoresAndSetsMultiUser()
    {
        // Arrange
        var filePath = @"backups\test_restore.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "restore content");

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, executorMock, loggerMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            executorMock
                .Setup(e => e.SetSingleUserModeAsync())
                .Returns(Task.CompletedTask);
            executorMock
                .Setup(e => e.RestoreDatabaseAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            executorMock
                .Setup(e => e.SetMultiUserModeAsync())
                .Returns(Task.CompletedTask);

            // Act
            await service.RestoreAsync(record.Id, confirm: true);

            // Assert — all 3 executor methods were called in order
            executorMock.Verify(e => e.SetSingleUserModeAsync(), Times.Once);
            executorMock.Verify(e => e.RestoreDatabaseAsync(filePath), Times.Once);
            executorMock.Verify(e => e.SetMultiUserModeAsync(), Times.Once);

            // Warning and info logs
            loggerMock.Verify(l => l.LogWarning(
                "Starting database restore from {FilePath} (ID: {Id})",
                It.IsAny<object?[]>()), Times.AtLeastOnce);
            loggerMock.Verify(l => l.LogInfo(
                "Database restore completed from {FilePath}",
                It.IsAny<object?[]>()), Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task RestoreAsync_ExecutorFails_SetsMultiUserAndRethrows()
    {
        // Arrange
        var filePath = @"backups\test_fail_restore.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "content");

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, executorMock, loggerMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            executorMock
                .Setup(e => e.SetSingleUserModeAsync())
                .Returns(Task.CompletedTask);
            executorMock
                .Setup(e => e.RestoreDatabaseAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Restore failed"));
            executorMock
                .Setup(e => e.SetMultiUserModeAsync())
                .Returns(Task.CompletedTask);

            // Act
            var act = () => service.RestoreAsync(record.Id, confirm: true);

            // Assert — exception rethrown AND multi-user set even after failure
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Restore failed");

            executorMock.Verify(e => e.SetSingleUserModeAsync(), Times.Once);
            executorMock.Verify(e => e.RestoreDatabaseAsync(filePath), Times.Once);
            executorMock.Verify(e => e.SetMultiUserModeAsync(), Times.Exactly(2)); // finally + catch

            // Error was logged
            loggerMock.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains("Restore")),
                It.IsAny<Exception>(),
                It.IsAny<object?[]>()), Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task RestoreAsync_SingleUserFails_SetsMultiUserAndRethrows()
    {
        // Arrange — SetSingleUserModeAsync itself fails
        var filePath = @"backups\test_fail_single_user.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "content");

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, executorMock, loggerMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            executorMock
                .Setup(e => e.SetSingleUserModeAsync())
                .ThrowsAsync(new InvalidOperationException("Cannot set single user"));
            executorMock
                .Setup(e => e.SetMultiUserModeAsync())
                .Returns(Task.CompletedTask);

            // Act
            var act = () => service.RestoreAsync(record.Id, confirm: true);

            // Assert — exception rethrown, multi-user called (outer catch), restore NOT called
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cannot set single user");

            executorMock.Verify(e => e.SetSingleUserModeAsync(), Times.Once);
            executorMock.Verify(e => e.RestoreDatabaseAsync(It.IsAny<string>()), Times.Never);
            executorMock.Verify(e => e.SetMultiUserModeAsync(), Times.Once); // outer catch

            loggerMock.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains("Restore") || s.Contains("single")),
                It.IsAny<Exception>(),
                It.IsAny<object?[]>()), Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    // ========================================================================
    // GetBackupHistoryAsync — Backup History
    // ========================================================================

    [Fact]
    public async Task GetBackupHistoryAsync_ReturnsRecordsOrderedByDateDescending()
    {
        // Arrange
        var oldRecord = CreateTestRecord(
            id: Guid.NewGuid(),
            filePath: "old.bak",
            createdAt: new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc));
        var newRecord = CreateTestRecord(
            id: Guid.NewGuid(),
            filePath: "new.bak",
            createdAt: new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc));
        var midRecord = CreateTestRecord(
            id: Guid.NewGuid(),
            filePath: "mid.bak",
            createdAt: new DateTime(2026, 7, 18, 18, 0, 0, DateTimeKind.Utc));

        var (service, _, _, _) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { oldRecord, newRecord, midRecord });

        // Act
        var result = await service.GetBackupHistoryAsync();

        // Assert — ordered by CreatedAt descending
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(newRecord.Id);      // July 19
        result[1].Id.Should().Be(midRecord.Id);       // July 18 18:00
        result[2].Id.Should().Be(oldRecord.Id);       // July 18 10:00
    }

    [Fact]
    public async Task GetBackupHistoryAsync_Empty_ReturnsEmpty()
    {
        // Arrange — no backup records
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetBackupHistoryAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // DeleteBackupAsync — Delete Backup Record & File
    // ========================================================================

    [Fact]
    public async Task DeleteBackupAsync_FileExists_DeletesFileAndCompletes()
    {
        // Arrange — create an actual file to delete
        var filePath = @"backups\test_delete.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "to be deleted");

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, _, loggerMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            // Act
            await service.DeleteBackupAsync(record.Id);

            // Assert — file was deleted
            File.Exists(filePath).Should().BeFalse();

            // Log was written
            loggerMock.Verify(l => l.LogInfo(
                "Backup record {Id} deleted",
                It.IsAny<object?[]>()), Times.Once);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DeleteBackupAsync_RecordNotFound_ThrowsFileNotFoundException()
    {
        // Arrange — no backup records
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.DeleteBackupAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteBackupAsync_FileNotFound_SkipsDelete()
    {
        // Arrange — record exists but file doesn't
        var record = CreateTestRecord(filePath: @"backups\nonexistent_delete.bak");
        var (service, _, _, loggerMock) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { record });

        // Act
        await service.DeleteBackupAsync(record.Id);

        // Assert — no exception, log written (delete skipped silently)
        loggerMock.Verify(l => l.LogInfo(
            "Backup record {Id} deleted",
            It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBackupAsync_FileDeleteFails_LogsWarning()
    {
        // Arrange — create a file and keep it locked so File.Delete fails
        var filePath = @"backups\locked_file.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Write and then open the file with exclusive lock
        File.WriteAllText(filePath, "locked content");
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, _, loggerMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            // Act — File.Delete on the locked file will throw IOException
            await service.DeleteBackupAsync(record.Id);

            // Assert — warning was logged, exception NOT rethrown
            loggerMock.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains("Could not delete")),
                It.IsAny<object?[]>()), Times.AtLeastOnce);

            // Info log still written after warning
            loggerMock.Verify(l => l.LogInfo(
                "Backup record {Id} deleted",
                It.IsAny<object?[]>()), Times.Once);
        }
        finally
        {
            fileStream.Dispose();
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    // ========================================================================
    // Retention Policy — EnforceRetentionPolicyAsync (called via CreateBackupAsync)
    // ========================================================================

    /// <summary>
    /// Seeds the given number of backup records into the mock repository and
    /// calls CreateBackupAsync (which internally calls EnforceRetentionPolicyAsync).
    /// Returns the service and logger mock so the caller can verify retention logs.
    /// </summary>
    private async Task<(
        BackupService Service,
        Mock<ILoggerService> LoggerMock,
        BackupRecord Result)> SeedAndCreateBackupAsync(
        int recordCount,
        bool createBackupFile = true)
    {
        var records = Enumerable.Range(0, recordCount)
            .Select(i => CreateTestRecord(
                id: Guid.NewGuid(),
                filePath: $@"backups\seed_{i}.bak",
                createdAt: DateTime.UtcNow.AddDays(-(i + 1))))
            .ToList();

        var (service, unitOfWorkMock, executorMock, loggerMock) = BuildServiceWithMocks(backupRecords: records);

        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                if (createBackupFile)
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(path, "retention test backup");
                }
            })
            .Returns(Task.CompletedTask);
        executorMock
            .Setup(e => e.VerifyBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await service.CreateBackupAsync();
        return (service, loggerMock, result);
    }

    [Fact]
    public async Task CreateBackupAsync_ExceedsCountRetention_EnforcesRetentionByCount()
    {
        // Arrange — seed 35 records, exceeding MaxBackupRetentionCount = 30
        // Act
        var (service, loggerMock, result) = await SeedAndCreateBackupAsync(recordCount: 35);

        // Assert — retention policy was applied for the 5 oldest records over 30
        loggerMock.Verify(l => l.LogInfo(
            "Retention policy applied: {Count} old backup(s) deleted",
            It.IsAny<object?[]>()), Times.AtLeastOnce);

        // New backup was created successfully
        result.Should().NotBeNull();
        result.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBackupAsync_UnderCountRetention_SkipsRetention()
    {
        // Arrange — seed 25 records, under MaxBackupRetentionCount = 30
        // Act
        var (service, loggerMock, result) = await SeedAndCreateBackupAsync(recordCount: 25);

        // Assert — retention by-count should NOT fire (only 25 records)
        // But age-based retention might fire if records are old enough.
        // Our records are 1-25 days old, well under 90-day cutoff.
        loggerMock.Verify(l => l.LogInfo(
            "Retention policy applied: {Count} old backup(s) deleted",
            It.IsAny<object?[]>()), Times.Never);

        result.Should().NotBeNull();
        result.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBackupAsync_RecordsExceedAgeRetention_EnforcesRetentionByAge()
    {
        // Arrange — seed records older than MaxBackupRetentionDays = 90
        var oldRecords = Enumerable.Range(0, 5)
            .Select(i => CreateTestRecord(
                id: Guid.NewGuid(),
                filePath: $@"backups\old_age_{i}.bak",
                createdAt: DateTime.UtcNow.AddDays(-(100 + i))))  // 100-104 days old
            .ToList();

        var freshRecords = Enumerable.Range(0, 5)
            .Select(i => CreateTestRecord(
                id: Guid.NewGuid(),
                filePath: $@"backups\fresh_{i}.bak",
                createdAt: DateTime.UtcNow.AddDays(-1)))
            .ToList();

        var allRecords = oldRecords.Concat(freshRecords).ToList();
        var (service, unitOfWorkMock, executorMock, loggerMock) = BuildServiceWithMocks(backupRecords: allRecords);

        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, "age retention test");
            })
            .Returns(Task.CompletedTask);
        executorMock
            .Setup(e => e.VerifyBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.CreateBackupAsync();

        // Assert — retention policy logged the 5 old records
        loggerMock.Verify(l => l.LogInfo(
            "Retention policy applied: {Count} old backup(s) deleted",
            It.IsAny<object?[]>()), Times.AtLeastOnce);

        result.Should().NotBeNull();
        result.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBackupAsync_RetentionGetAllThrows_OuterCatchLogsError()
    {
        // Arrange — we need the retention policy's GetAllAsync to throw AFTER
        // the initial CreateBackupAsync flow completes (backup, verify, save record).
        //
        // EnforceRetentionPolicyAsync is called at the END of CreateBackupAsync.
        // It calls _unitOfWork.BackupRecords.GetAllAsync() internally.
        // By setting up the mock to throw, we exercise the outer try/catch.
        //
        // However, CreateBackupAsync does NOT call GetAllAsync before retention
        // (it only calls AddAsync). So we can safely mock GetAllAsync to throw
        // without breaking the backup creation flow.
        var mockRecords = new List<BackupRecord>
        {
            CreateTestRecord(id: Guid.NewGuid(), filePath: "existing.bak")
        };

        var (service, unitOfWorkMock, executorMock, loggerMock) = BuildServiceWithMocks(backupRecords: mockRecords);

        // Setup executor for successful backup
        executorMock
            .Setup(e => e.BackupDatabaseAsync(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, "retention catch test");
            })
            .Returns(Task.CompletedTask);
        executorMock
            .Setup(e => e.VerifyBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Now make the repository's GetAllAsync throw — this will be hit when
        // EnforceRetentionPolicyAsync calls it at the end of CreateBackupAsync
        var backupRepoMock = Mock.Get(unitOfWorkMock.Object.BackupRecords);
        backupRepoMock
            .Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("DB connection lost during retention"));

        // Act — the retention outer catch should swallow the error
        var result = await service.CreateBackupAsync();

        // Assert — backup was created successfully (retention failure is non-fatal)
        result.Should().NotBeNull();
        result.IsVerified.Should().BeTrue();

        // The retention error was logged by the outer catch
        loggerMock.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("retention policy") || s.Contains("Failed to enforce")),
            It.IsAny<Exception?>(),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }
}

