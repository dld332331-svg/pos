using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IShiftService
{
    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, Guid userId);
    Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, Guid userId);
    Task<ShiftDto?> GetCurrentShiftAsync(Guid userId);
    Task<List<ShiftDto>> GetShiftHistoryAsync(DateTime? from, DateTime? to);
    Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid shiftId);
    Task<CashReportDto> GetCashReportAsync(Guid shiftId);
}