using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IBackupManagementService
{
    Task<BackupDto> CreateBackupAsync(Guid userId);
    Task<OperationResult> RestoreBackupAsync(Guid backupId, Guid userId);
    Task<List<BackupDto>> GetBackupHistoryAsync();
    Task<OperationResult> DeleteBackupAsync(Guid backupId);
}