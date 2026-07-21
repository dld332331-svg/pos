using POS.Application.DTOs;

namespace POS.Application.Services;

public interface ISettingsService
{
    Task<string?> GetSettingAsync(string key);
    Task<OperationResult> SetSettingAsync(string key, string value, Guid userId);
    Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category);
}