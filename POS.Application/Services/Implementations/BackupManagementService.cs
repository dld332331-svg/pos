using POS.Application.DTOs;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class BackupManagementService : IBackupManagementService
{
    private readonly IBackupService _backupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public BackupManagementService(IBackupService backupService, IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _backupService = backupService;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<BackupDto> CreateBackupAsync(Guid userId)
    {
        var record = await _backupService.CreateBackupAsync();

        await _auditService.LogAsync(userId, AuditActionType.BackupPerformed, "BackupRecord", record.Id,
            null, $"FilePath={record.FilePath},Size={record.FileSize}", null);

        return new BackupDto(
            record.Id,
            record.FilePath,
            record.FileSize,
            record.CreatedAt,
            record.IsVerified,
            record.RestoreCount);
    }

    public async Task<OperationResult> RestoreBackupAsync(Guid backupId, Guid userId)
    {
        var record = await _unitOfWork.BackupRecords.GetByIdAsync(backupId);
        if (record is null)
            return new OperationResult(false, ErrorMessage: "سجل النسخة الاحتياطية غير موجود");

        if (string.IsNullOrWhiteSpace(record.FilePath))
            return new OperationResult(false, ErrorMessage: "مسار ملف النسخة الاحتياطية غير صالح");

        await _backupService.RestoreAsync(record.Id, confirm: true);

        record.RestoreCount++;

        await _auditService.LogAsync(userId, AuditActionType.RestorePerformed, "BackupRecord", backupId,
            null, $"FilePath={record.FilePath},RestoreCount={record.RestoreCount}", null);

        return new OperationResult(true, SuccessMessage: "تم استعادة النسخة الاحتياطية بنجاح");
    }

    public async Task<List<BackupDto>> GetBackupHistoryAsync()
    {
        var records = await _unitOfWork.BackupRecords.GetAllAsync();

        return records
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new BackupDto(
                r.Id,
                r.FilePath,
                r.FileSize,
                r.CreatedAt,
                r.IsVerified,
                r.RestoreCount))
            .ToList();
    }

    public async Task<OperationResult> DeleteBackupAsync(Guid backupId)
    {
        var record = await _unitOfWork.BackupRecords.GetByIdAsync(backupId);
        if (record is null)
            return new OperationResult(false, ErrorMessage: "سجل النسخة الاحتياطية غير موجود");

        // Attempt to delete the physical file
        try
        {
            if (File.Exists(record.FilePath))
            {
                File.Delete(record.FilePath);
            }
        }
        catch (Exception)
        {
            // Log but don't fail - the DB record should still be removed
        }

        await _unitOfWork.BackupRecords.DeleteAsync(record);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.BackupPerformed, "BackupRecord", backupId,
            $"FilePath={record.FilePath}", null, "Backup record deleted");

        return new OperationResult(true, SuccessMessage: "تم حذف النسخة الاحتياطية بنجاح");
    }
}