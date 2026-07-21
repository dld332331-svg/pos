using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(DateTime? from, DateTime? to, string? actionType, string? entityName, int page = 1, int pageSize = 50);
}