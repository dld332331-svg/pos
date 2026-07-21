using POS.Application.DTOs;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class AuditQueryService : IAuditQueryService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditQueryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        DateTime? from, DateTime? to, string? actionType, string? entityName,
        int page = 1, int pageSize = 50)
    {
        var allLogs = (await _unitOfWork.AuditLogs.GetAllAsync()).AsQueryable();

        if (from.HasValue)
            allLogs = allLogs.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            allLogs = allLogs.Where(l => l.Timestamp <= to.Value.AddDays(1));

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var actionUpper = actionType.Trim().ToUpper();
            allLogs = allLogs.Where(l => l.ActionType.ToString().ToUpper().Contains(actionUpper));
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var entityUpper = entityName.Trim().ToUpper();
            allLogs = allLogs.Where(l => l.EntityName.ToUpper().Contains(entityUpper));
        }

        var total = allLogs.Count();

        var users = await _unitOfWork.Users.GetAllAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.FullName);

        var items = allLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(l => new AuditLogDto(
                l.Timestamp,
                l.UserId.HasValue && userMap.ContainsKey(l.UserId.Value)
                    ? userMap[l.UserId.Value]
                    : "System",
                l.ActionType.ToString(),
                l.EntityName,
                l.EntityId?.ToString() ?? string.Empty,
                l.BeforeValue,
                l.AfterValue,
                l.Reason))
            .ToList();

        return new PagedResult<AuditLogDto>(items, total, page, pageSize);
    }
}