#nullable enable

using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for BackupManagementService covering all 4 public methods:
/// CreateBackupAsync, RestoreBackupAsync, GetBackupHistoryAsync, DeleteBackupAsync.
/// Verifies audit logging, error handling, and delegation to IBackupService.
/// </summary>
public class BackupManagementServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static BackupRecord CreateTestRecord(
        Guid? id = null,
        string filePath = @"backups\test.bak",
        long fileSize = 1024,
        int restoreCount = 0)
    {
        return new BackupRecord
        {
            Id = id ?? Guid.NewGuid(),
            FilePath = filePath,
            FileSize = fileSize,
            CreatedAt = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc),
            IsVerified = false,
            RestoreCount = restoreCount
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Builds a BackupManagementService with fully mocked dependencies.
    /// </summary>
    private (BackupManagementService service, Mock<IBackupService> backupServiceMock, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            List<BackupRecord>? backupRecords = null)
    {
        var backupServiceMock = new Mock<IBackupService>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        // Default audit setup
        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // BackupRecords repository (ISimpleRepository)
        var backupRepoMock = new Mock<ISimpleRepository<BackupRecord>>();
        backupRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(backupRecords ?? new List<BackupRecord>());
        backupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => backupRecords?.FirstOrDefault(b => b.Id == id));
        unitOfWorkMock.Setup(u => u.BackupRecords).Returns(backupRepoMock.Object);

        // Default BackupService setup
        backupServiceMock
            .Setup(b => b.CreateBackupAsync(It.IsAny<string?>()))
            .ReturnsAsync(CreateTestRecord());
        backupServiceMock
            .Setup(b => b.RestoreAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        var service = new BackupManagementService(
            backupServiceMock.Object, unitOfWorkMock.Object, auditServiceMock.Object);
        return (service, backupServiceMock, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // CreateBackupAsync
    // ========================================================================

    [Fact]
    public async Task CreateBackupAsync_Success_CreatesBackupAndLogsAudit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var backupRecord = CreateTestRecord(id: recordId, fileSize: 2048);

        var (service, backupServiceMock, _, auditServiceMock) = BuildServiceWithMocks();

        backupServiceMock
            .Setup(b => b.CreateBackupAsync(It.IsAny<string?>()))
            .ReturnsAsync(backupRecord);

        // Act
        var result = await service.CreateBackupAsync(userId);

        // Assert — returned DTO
        result.Should().NotBeNull();
        result.Id.Should().Be(recordId);
        result.FileSize.Should().Be(2048);
        result.FilePath.Should().Be(@"backups\test.bak");
        result.IsVerified.Should().BeFalse();
        result.RestoreCount.Should().Be(0);

        // BackupService.CreateBackupAsync was called
        backupServiceMock.Verify(b => b.CreateBackupAsync(null), Times.Once);

        // Audit was logged with BackupPerformed
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.BackupPerformed,
            "BackupRecord",
            recordId,
            null,
            It.Is<string>(s => s.Contains("FilePath=") && s.Contains("Size=2048")),
            null), Times.Once);
    }

    // ========================================================================
    // RestoreBackupAsync
    // ========================================================================

    [Fact]
    public async Task RestoreBackupAsync_Success_RestoresAndIncrementsRestoreCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var record = CreateTestRecord(restoreCount: 2);
        var (service, backupServiceMock, _, auditServiceMock) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { record });

        // Act
        var result = await service.RestoreBackupAsync(record.Id, userId);

        // Assert — success result
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Be("تم استعادة النسخة الاحتياطية بنجاح");

        // RestoreCount was incremented
        record.RestoreCount.Should().Be(3);

        // BackupService.RestoreAsync was called with confirm: true
        backupServiceMock.Verify(b => b.RestoreAsync(record.Id, true), Times.Once);

        // Audit was logged with RestorePerformed
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.RestorePerformed,
            "BackupRecord",
            record.Id,
            null,
            It.Is<string>(s => s.Contains("FilePath=") && s.Contains("RestoreCount=3")),
            null), Times.Once);
    }

    [Fact]
    public async Task RestoreBackupAsync_RecordNotFound_ReturnsFailure()
    {
        // Arrange — no backup records
        var (service, backupServiceMock, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.RestoreBackupAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert — failure with Arabic error message
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("سجل النسخة الاحتياطية غير موجود");

        // BackupService.RestoreAsync was NOT called
        backupServiceMock.Verify(b => b.RestoreAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RestoreBackupAsync_EmptyFilePath_ReturnsFailure()
    {
        // Arrange — record exists but has empty FilePath
        var record = CreateTestRecord(filePath: "");
        var (service, backupServiceMock, _, _) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { record });

        // Act
        var result = await service.RestoreBackupAsync(record.Id, Guid.NewGuid());

        // Assert — failure with Arabic error message
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("مسار ملف النسخة الاحتياطية غير صالح");

        // BackupService.RestoreAsync was NOT called
        backupServiceMock.Verify(b => b.RestoreAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RestoreBackupAsync_WhitespaceFilePath_ReturnsFailure()
    {
        // Arrange — record with whitespace-only FilePath
        var record = CreateTestRecord(filePath: "   ");
        var (service, backupServiceMock, _, _) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { record });

        // Act
        var result = await service.RestoreBackupAsync(record.Id, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("مسار ملف النسخة الاحتياطية غير صالح");
        backupServiceMock.Verify(b => b.RestoreAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    // ========================================================================
    // GetBackupHistoryAsync
    // ========================================================================

    [Fact]
    public async Task GetBackupHistoryAsync_ReturnsRecordsOrderedByDateDescending()
    {
        // Arrange
        var oldRecord = CreateTestRecord(
            id: Guid.NewGuid(),
            filePath: "old.bak");
        oldRecord.CreatedAt = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc);
        var newRecord = CreateTestRecord(
            id: Guid.NewGuid(),
            filePath: "new.bak");
        newRecord.CreatedAt = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);

        var (service, _, _, _) = BuildServiceWithMocks(
            backupRecords: new List<BackupRecord> { oldRecord, newRecord });

        // Act
        var result = await service.GetBackupHistoryAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(newRecord.Id);
        result[1].Id.Should().Be(oldRecord.Id);
    }

    [Fact]
    public async Task GetBackupHistoryAsync_Empty_ReturnsEmpty()
    {
        // Arrange
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetBackupHistoryAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // DeleteBackupAsync
    // ========================================================================

    [Fact]
    public async Task DeleteBackupAsync_RecordNotFound_ReturnsFailure()
    {
        // Arrange — no backup records
        var (service, _, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.DeleteBackupAsync(Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("سجل النسخة الاحتياطية غير موجود");
    }

    [Fact]
    public async Task DeleteBackupAsync_Success_DeletesFileAndLogsAudit()
    {
        // Arrange — create an actual file to delete
        var filePath = @"backups\delete_me.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "to be deleted");

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, _, auditServiceMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            // Act
            var result = await service.DeleteBackupAsync(record.Id);

            // Assert — success
            result.Success.Should().BeTrue();
            result.SuccessMessage.Should().Be("تم حذف النسخة الاحتياطية بنجاح");

            // File was deleted
            File.Exists(filePath).Should().BeFalse();

            // Audit was logged with before value
            auditServiceMock.Verify(a => a.LogAsync(
                null,
                AuditActionType.BackupPerformed,
                "BackupRecord",
                record.Id,
                It.Is<string>(s => s.Contains("FilePath=")),
                null,
                "Backup record deleted"), Times.Once);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DeleteBackupAsync_FileDeleteFails_StillReturnsSuccess()
    {
        // Arrange — file exists but is locked
        var filePath = @"backups\locked_delete.bak";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, "locked content");

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            var record = CreateTestRecord(filePath: filePath);
            var (service, _, _, auditServiceMock) = BuildServiceWithMocks(
                backupRecords: new List<BackupRecord> { record });

            // Act — file delete fails (locked), but the operation still succeeds
            var result = await service.DeleteBackupAsync(record.Id);

            // Assert — success even though file delete failed
            result.Success.Should().BeTrue();
            result.SuccessMessage.Should().Be("تم حذف النسخة الاحتياطية بنجاح");

            // Audit was still logged
            auditServiceMock.Verify(a => a.LogAsync(
                null,
                AuditActionType.BackupPerformed,
                "BackupRecord",
                record.Id,
                It.IsAny<string>(),
                null,
                "Backup record deleted"), Times.Once);
        }
        finally
        {
            fileStream.Dispose();
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
