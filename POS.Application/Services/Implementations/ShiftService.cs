using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ShiftService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate no open shift exists for this user
        var existingOpen = (await _unitOfWork.Shifts.FindAsync(
            s => s.UserId == userId && s.Status == ShiftStatus.Open)).FirstOrDefault();

        if (existingOpen is not null)
            throw new InvalidOperationException("يوجد وردية مفتوحة بالفعل لهذا المستخدم");

        // Validate register exists
        var register = await _unitOfWork.Registers.GetByIdAsync(request.RegisterId)
            ?? throw new InvalidOperationException("الجهاز غير موجود");

        // Get next shift number for this register
        var allShifts = await _unitOfWork.Shifts.GetAllAsync();
        var maxShiftNum = allShifts
            .Where(s => s.RegisterId == request.RegisterId)
            .Max(s => (int?)s.ShiftNumber) ?? 0;

        var shift = new Shift
        {
            ShiftNumber = maxShiftNum + 1,
            UserId = userId,
            RegisterId = request.RegisterId,
            OpeningCash = MoneyPolicy.RoundToJOD(request.OpeningCash),
            TotalSales = 0,
            TotalReturns = 0,
            TotalExpenses = 0,
            TotalDeposits = 0,
            TotalWithdrawals = 0,
            Status = ShiftStatus.Open,
            OpenedAt = DateTime.UtcNow
        };

        await _unitOfWork.Shifts.AddAsync(shift);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(userId, AuditActionType.ShiftOpened, "Shift", shift.Id,
            null, $"OpeningCash={request.OpeningCash},Register={register.Name}", null);

        return await MapToDtoAsync(shift);
    }

    public async Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shift = await _unitOfWork.Shifts.GetByIdAsync(request.ShiftId)
            ?? throw new InvalidOperationException("الوردية غير موجودة");

        if (shift.Status != ShiftStatus.Open)
            throw new InvalidOperationException("الوردية ليست مفتوحة");

        // Calculate expected cash:
        // OpeningCash + CashSales - Expenses + Deposits - Withdrawals
        var sales = (await _unitOfWork.Sales.FindAsync(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed)).ToList();
        var payments = (await _unitOfWork.Payments.FindAsync(p => sales.Select(s => s.Id).Contains(p.SaleId))).ToList();

        var cashSales = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Cash).Sum(p => p.Amount));
        var cardSales = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Card).Sum(p => p.Amount));

        var expenses = (await _unitOfWork.Expenses.FindAsync(e => e.ShiftId == shift.Id)).ToList();
        var totalExpenses = MoneyPolicy.RoundToJOD(expenses.Sum(e => e.Amount));

        var wdList = (await _unitOfWork.WithdrawalDeposits.FindAsync(w => w.ShiftId == shift.Id)).ToList();
        var totalWithdrawals = MoneyPolicy.RoundToJOD(
            wdList.Where(w => w.Type == WithdrawalDepositType.Withdrawal).Sum(w => w.Amount));
        var totalDeposits = MoneyPolicy.RoundToJOD(
            wdList.Where(w => w.Type == WithdrawalDepositType.Deposit).Sum(w => w.Amount));

        var returns = (await _unitOfWork.Returns.FindAsync(r =>
            sales.Select(s => s.Id).Contains(r.OriginalSaleId))).ToList();
        var totalReturns = MoneyPolicy.RoundToJOD(returns.Sum(r => r.TotalAmount));

        var expectedCash = MoneyPolicy.RoundToJOD(
            shift.OpeningCash + cashSales - totalExpenses - totalWithdrawals + totalDeposits);

        shift.ClosingCash = MoneyPolicy.RoundToJOD(request.ActualCash);
        shift.ExpectedCash = expectedCash;
        shift.ActualCash = MoneyPolicy.RoundToJOD(request.ActualCash);
        shift.Variance = MoneyPolicy.RoundToJOD(request.ActualCash - expectedCash);
        shift.TotalSales = MoneyPolicy.RoundToJOD(cashSales + cardSales);
        shift.TotalReturns = totalReturns;
        shift.TotalExpenses = totalExpenses;
        shift.TotalDeposits = totalDeposits;
        shift.TotalWithdrawals = totalWithdrawals;
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAt = DateTime.UtcNow;
        shift.MarkAsModified(userId);

        await _unitOfWork.Shifts.UpdateAsync(shift);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(userId, AuditActionType.ShiftClosed, "Shift", shift.Id,
            $"Status=Open", $"Status=Closed,Expected={expectedCash},Actual={request.ActualCash},Variance={shift.Variance}", null);

        return await MapToDtoAsync(shift);
    }

    public async Task<ShiftDto?> GetCurrentShiftAsync(Guid userId)
    {
        var shift = (await _unitOfWork.Shifts.FindAsync(
            s => s.UserId == userId && s.Status == ShiftStatus.Open)).FirstOrDefault();

        if (shift is null) return null;

        return await MapToDtoAsync(shift);
    }

    public async Task<List<ShiftDto>> GetShiftHistoryAsync(DateTime? from, DateTime? to)
    {
        var allShifts = await _unitOfWork.Shifts.GetAllAsync();
        var filtered = allShifts.AsQueryable();

        if (from.HasValue)
            filtered = filtered.Where(s => s.OpenedAt >= from.Value);
        if (to.HasValue)
            filtered = filtered.Where(s => s.OpenedAt <= to.Value.AddDays(1));

        var result = new List<ShiftDto>();
        foreach (var s in filtered.OrderByDescending(s => s.OpenedAt))
        {
            result.Add(await MapToDtoAsync(s));
        }
        return result;
    }

    public async Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid shiftId)
    {
        var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId)
            ?? throw new InvalidOperationException("الوردية غير موجودة");

        var sales = (await _unitOfWork.Sales.FindAsync(s => s.ShiftId == shiftId && s.Status == SaleStatus.Completed)).ToList();
        var payments = (await _unitOfWork.Payments.FindAsync(p => sales.Select(s => s.Id).Contains(p.SaleId))).ToList();

        var totalCashSales = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Cash).Sum(p => p.Amount));
        var totalCardSales = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Card).Sum(p => p.Amount));

        var returns = (await _unitOfWork.Returns.FindAsync(r =>
            sales.Select(s => s.Id).Contains(r.OriginalSaleId))).ToList();
        var totalReturns = MoneyPolicy.RoundToJOD(returns.Sum(r => r.TotalAmount));

        return new ShiftSummaryDto(
            totalCashSales,
            totalCardSales,
            MoneyPolicy.RoundToJOD(totalCashSales + totalCardSales),
            sales.Count,
            returns.Count);
    }

    public async Task<CashReportDto> GetCashReportAsync(Guid shiftId)
    {
        var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId)
            ?? throw new InvalidOperationException("الوردية غير موجودة");

        var sales = (await _unitOfWork.Sales.FindAsync(s => s.ShiftId == shiftId && s.Status == SaleStatus.Completed)).ToList();
        var payments = (await _unitOfWork.Payments.FindAsync(p => sales.Select(s => s.Id).Contains(p.SaleId))).ToList();

        var totalCashPayments = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Cash).Sum(p => p.Amount));
        var totalCardPayments = MoneyPolicy.RoundToJOD(
            payments.Where(p => p.PaymentMethod == PaymentMethod.Card).Sum(p => p.Amount));

        var expenses = (await _unitOfWork.Expenses.FindAsync(e => e.ShiftId == shiftId)).ToList();
        var totalExpenses = MoneyPolicy.RoundToJOD(expenses.Sum(e => e.Amount));

        var wdList = (await _unitOfWork.WithdrawalDeposits.FindAsync(w => w.ShiftId == shiftId)).ToList();
        var totalWithdrawals = MoneyPolicy.RoundToJOD(
            wdList.Where(w => w.Type == WithdrawalDepositType.Withdrawal).Sum(w => w.Amount));
        var totalDeposits = MoneyPolicy.RoundToJOD(
            wdList.Where(w => w.Type == WithdrawalDepositType.Deposit).Sum(w => w.Amount));

        // If shift not yet closed, compute expected dynamically
        decimal expectedCash;
        if (shift.ExpectedCash.HasValue)
        {
            expectedCash = shift.ExpectedCash.Value;
        }
        else
        {
            expectedCash = MoneyPolicy.RoundToJOD(
                shift.OpeningCash + totalCashPayments - totalExpenses - totalWithdrawals + totalDeposits);
        }

        return new CashReportDto(
            expectedCash,
            shift.ActualCash ?? 0,
            shift.Variance ?? 0,
            totalCashPayments,
            totalCardPayments,
            totalExpenses,
            totalWithdrawals,
            totalDeposits);
    }

    private async Task<ShiftDto> MapToDtoAsync(Shift shift)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(shift.UserId);
        var register = await _unitOfWork.Registers.GetByIdAsync(shift.RegisterId);

        return new ShiftDto(
            shift.Id,
            shift.ShiftNumber,
            user?.FullName ?? "Unknown",
            register?.Name ?? "Unknown",
            shift.OpeningCash,
            shift.ClosingCash,
            shift.TotalSales,
            shift.TotalReturns,
            shift.TotalExpenses,
            shift.ExpectedCash,
            shift.ActualCash,
            shift.Variance,
            shift.Status.ToString(),
            shift.OpenedAt,
            shift.ClosedAt);
    }
}