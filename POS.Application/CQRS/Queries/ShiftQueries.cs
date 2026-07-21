using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

public record GetActiveShiftQuery(Guid UserId) : IQuery<ShiftDto?>;
public record GetShiftHistoryQuery(DateTime? From, DateTime? To) : IQuery<List<ShiftDto>>;
public record GetShiftSummaryQuery(Guid ShiftId) : IQuery<ShiftSummaryDto>;

public sealed class GetActiveShiftQueryHandler(IShiftService service) : IQueryHandler<GetActiveShiftQuery, ShiftDto?>
{
    public Task<ShiftDto?> HandleAsync(GetActiveShiftQuery q, CancellationToken ct = default)
        => service.GetCurrentShiftAsync(q.UserId);
}

public sealed class GetShiftHistoryQueryHandler(IShiftService service) : IQueryHandler<GetShiftHistoryQuery, List<ShiftDto>>
{
    public Task<List<ShiftDto>> HandleAsync(GetShiftHistoryQuery q, CancellationToken ct = default)
        => service.GetShiftHistoryAsync(q.From, q.To);
}

public sealed class GetShiftSummaryQueryHandler(IShiftService service) : IQueryHandler<GetShiftSummaryQuery, ShiftSummaryDto>
{
    public Task<ShiftSummaryDto> HandleAsync(GetShiftSummaryQuery q, CancellationToken ct = default)
        => service.GetShiftSummaryAsync(q.ShiftId);
}
