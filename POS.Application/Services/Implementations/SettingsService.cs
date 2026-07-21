using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public SettingsService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var settings = await _unitOfWork.Settings.FindAsync(s => s.Key == key);
        var setting = settings.FirstOrDefault();
        return setting?.Value;
    }

    public async Task<OperationResult> SetSettingAsync(string key, string value, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new OperationResult(false, ErrorMessage: "مفتاح الإعداد مطلوب");

        var existing = (await _unitOfWork.Settings.FindAsync(s => s.Key == key)).FirstOrDefault();

        if (existing is not null)
        {
            var beforeValue = existing.Value;
            existing.Value = value;
            existing.MarkAsModified(userId);

            await _unitOfWork.Settings.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync();

            await _auditService.LogAsync(userId, AuditActionType.SettingChanged, "Setting", existing.Id,
                beforeValue, value, null);
        }
        else
        {
            // Infer category from key (e.g., "Tax.DefaultRate" -> "Tax")
            var category = key.Contains('.') ? key.Split('.')[0] : "General";

            var setting = new Setting
            {
                Key = key,
                Value = value,
                Category = category
            };

            await _unitOfWork.Settings.AddAsync(setting);
            await _unitOfWork.SaveChangesAsync();

            await _auditService.LogAsync(userId, AuditActionType.SettingChanged, "Setting", setting.Id,
                null, value, null);
        }

        return new OperationResult(true, SuccessMessage: "تم حفظ الإعداد بنجاح");
    }

    public async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return new Dictionary<string, string>();

        var settings = await _unitOfWork.Settings.FindAsync(s => s.Category == category);
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }
}