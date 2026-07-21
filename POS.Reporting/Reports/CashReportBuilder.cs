namespace POS.Reporting.Reports;
using POS.Application.DTOs;

#region Supporting Records

public record CashExpenseEntry(DateTime Date, string Reason, decimal Amount);
public record CashWithdrawalEntry(DateTime Date, decimal Amount, string? Note);
public record CashDepositEntry(DateTime Date, decimal Amount, string? Note);
public record CashReturnEntry(string TransactionId, DateTime Date, string Reason, decimal Amount);
public record ShiftInfoDto(string ShiftId, string CashierName, DateTime OpenTime, DateTime? CloseTime, decimal OpeningCash);

#endregion

/// <summary>
/// Builds a detailed Arabic cash report for shift closing with all financial sections.
/// </summary>
public class CashReportBuilder
{
    #region Main Report Method

    public byte[] BuildCashReport(
        CashReportDto data,
        string businessName,
        DateTime shiftDate,
        ShiftInfoDto? shiftInfo = null,
        List<CashExpenseEntry>? expenses = null,
        List<CashWithdrawalEntry>? withdrawals = null,
        List<CashDepositEntry>? deposits = null,
        List<CashReturnEntry>? returns = null,
        string userName = "")
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        expenses ??= new List<CashExpenseEntry>();
        withdrawals ??= new List<CashWithdrawalEntry>();
        deposits ??= new List<CashDepositEntry>();
        returns ??= new List<CashReturnEntry>();

        var totalReturns = returns.Sum(r => r.Amount);
        var separator = new string('=', 60);
        var thinSep = new string('-', 60);
        var dotSep = new string('.', 60);

        // ===================================================
        // HEADER
        // ===================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                   تقرير النقدية - {businessName}");
        sb.AppendLine($"                         التاريخ: {shiftDate:yyyy/MM/dd}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===================================================
        // SHIFT INFO SECTION
        // ===================================================
        if (shiftInfo != null)
        {
            sb.AppendLine("┌─────────────────────────────────────────────────────────ف");
            sb.AppendLine("│                   معلومات الوردية                      │");
            sb.AppendLine("├─────────────────────────────────────────────────────────┤");
            sb.AppendLine($"│ رقم الوردية:     {shiftInfo.ShiftId,-36} │");
            sb.AppendLine($"│ الموظف:          {shiftInfo.CashierName,-36} │");
            sb.AppendLine($"│ وقت الافتتاح:    {shiftInfo.OpenTime:HH:mm,-36} │");
            sb.AppendLine($"│ وقت الإغلاق:     {(shiftInfo.CloseTime?.ToString("HH:mm") ?? "لم يغلق بعد"),-36} │");
            sb.AppendLine("└─────────────────────────────────────────────────────────┘");
            sb.AppendLine();
        }

        // ===================================================
        // OPENING CASH SECTION
        // ===================================================
        decimal openingCash = shiftInfo?.OpeningCash ?? 0m;
        sb.AppendLine("┌─────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                    الصندوق الافتتاحي                    │");
        sb.AppendLine("├─────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│ المبلغ الافتتاحي:                      {openingCash,10:0.000} JOD  │");
        sb.AppendLine("└─────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===================================================
        // CASH SALES BREAKDOWN
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                       تفصيل المبيعات النقدية");
        sb.AppendLine(dotSep);
        sb.AppendLine($"  {"إجمالي المبيعات النقدية:",-40} {data.TotalCashPayments,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // CARD SALES BREAKDOWN
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                       تفصيل مبيعات البطاقات");
        sb.AppendLine(dotSep);
        sb.AppendLine($"  {"إجمالي مبيعات البطاقات:",-40} {data.TotalCardPayments,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // RETURNS SECTION
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                          المرتجعات");
        sb.AppendLine(dotSep);

        if (returns.Count > 0)
        {
            sb.AppendLine($"  {"رقم العملية",-15} {"التاريخ",-12} {"السبب",-20} {"المبلغ",12}");
            sb.AppendLine($"  {new string('-', 15)} {new string('-', 12)} {new string('-', 20)} {new string('-', 12)}");
            foreach (var ret in returns)
            {
                sb.AppendLine($"  {ret.TransactionId,-15} {ret.Date:HH:mm,-12} {ret.Reason,-20} {ret.Amount,12:0.000}");
            }
            sb.AppendLine(thinSep);
        }

        sb.AppendLine($"  {"عدد المرتجعات:",-40} {returns.Count,12}");
        sb.AppendLine($"  {"إجمالي المرتجعات:",-40} {totalReturns,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // EXPENSES SECTION
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                          المصروفات");
        sb.AppendLine(dotSep);

        if (expenses.Count > 0)
        {
            sb.AppendLine($"  {"التاريخ",-12} {"السبب",-30} {"المبلغ",12}");
            sb.AppendLine($"  {new string('-', 12)} {new string('-', 30)} {new string('-', 12)}");
            foreach (var exp in expenses)
            {
                sb.AppendLine($"  {exp.Date:HH:mm,-12} {exp.Reason,-30} {exp.Amount,12:0.000}");
            }
            sb.AppendLine(thinSep);
        }
        else
        {
            sb.AppendLine("  لا توجد مصروفات مسجلة");
        }

        sb.AppendLine($"  {"إجمالي المصروفات:",-40} {data.TotalExpenses,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // WITHDRAWALS SECTION
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                          السحوبات");
        sb.AppendLine(dotSep);

        if (withdrawals.Count > 0)
        {
            sb.AppendLine($"  {"التاريخ",-12} {"ملاحظات",-30} {"المبلغ",12}");
            sb.AppendLine($"  {new string('-', 12)} {new string('-', 30)} {new string('-', 12)}");
            foreach (var w in withdrawals)
            {
                sb.AppendLine($"  {w.Date:HH:mm,-12} {(w.Note ?? "-"),-30} {w.Amount,12:0.000}");
            }
            sb.AppendLine(thinSep);
        }
        else
        {
            sb.AppendLine("  لا توجد سحوبات مسجلة");
        }

        sb.AppendLine($"  {"إجمالي السحوبات:",-40} {data.TotalWithdrawals,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // DEPOSITS SECTION
        // ===================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                          الإيداعات");
        sb.AppendLine(dotSep);

        if (deposits.Count > 0)
        {
            sb.AppendLine($"  {"التاريخ",-12} {"ملاحظات",-30} {"المبلغ",12}");
            sb.AppendLine($"  {new string('-', 12)} {new string('-', 30)} {new string('-', 12)}");
            foreach (var d in deposits)
            {
                sb.AppendLine($"  {d.Date:HH:mm,-12} {(d.Note ?? "-"),-30} {d.Amount,12:0.000}");
            }
            sb.AppendLine(thinSep);
        }
        else
        {
            sb.AppendLine("  لا توجد إيداعات مسجلة");
        }

        sb.AppendLine($"  {"إجمالي الإيداعات:",-40} {data.TotalDeposits,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // EXPECTED vs ACTUAL CASH CALCULATION
        // ===================================================
        decimal expectedCash = openingCash
            + data.TotalCashPayments
            - totalReturns
            - data.TotalExpenses
            - data.TotalWithdrawals
            + data.TotalDeposits;

        sb.AppendLine(separator);
        sb.AppendLine("              حساب النقدية المتوقعة مقابل الفعلية");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine($"  {"الصندوق الافتتاحي:",-40} {openingCash,12:0.000} JOD");
        sb.AppendLine($"  {"(+) المبيعات النقدية:",-40} {data.TotalCashPayments,12:0.000} JOD");
        sb.AppendLine($"  {"(-) المرتجعات النقدية:",-40} {totalReturns,12:0.000} JOD");
        sb.AppendLine($"  {"(-) المصروفات:",-40} {data.TotalExpenses,12:0.000} JOD");
        sb.AppendLine($"  {"(-) السحوبات:",-40} {data.TotalWithdrawals,12:0.000} JOD");
        sb.AppendLine($"  {"(+) الإيداعات:",-40} {data.TotalDeposits,12:0.000} JOD");
        sb.AppendLine(thinSep);
        sb.AppendLine($"  {"المبلغ المتوقع في الصندوق:",-40} {expectedCash,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // VARIANCE WITH COLOR CODING
        // ===================================================
        decimal variance = data.ActualCash - expectedCash;

        sb.AppendLine(separator);
        sb.AppendLine("                        المطابقة والفرق");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine($"  {"المبلغ المتوقع:",-40} {expectedCash,12:0.000} JOD");
        sb.AppendLine($"  {"المبلغ الفعلي:",-40} {data.ActualCash,12:0.000} JOD");
        sb.AppendLine(thinSep);

        if (variance == 0)
        {
            sb.AppendLine($"  [مطابق] الفرق:                                      {variance,12:0.000} JOD");
        }
        else if (variance > 0)
        {
            sb.AppendLine($"  [زيادة] الفقـد:                                      {variance,12:0.000} JOD");
        }
        else
        {
            sb.AppendLine($"  [نقص]   الفقـد:                                      {Math.Abs(variance),12:0.000} JOD");
        }

        sb.AppendLine();

        // ===================================================
        // SUMMARY TOTALS
        // ===================================================
        decimal totalSalesAll = data.TotalCashPayments + data.TotalCardPayments;

        sb.AppendLine(separator);
        sb.AppendLine("                         ملخص إجمالي");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine($"  {"إجمالي المبيعات (نقدي + بطاقة):",-40} {totalSalesAll,12:0.000} JOD");
        sb.AppendLine($"  {"  ├─ مبيعات نقدية:",-40} {data.TotalCashPayments,12:0.000} JOD");
        sb.AppendLine($"  {"  └─ مبيعات بطاقة:",-40} {data.TotalCardPayments,12:0.000} JOD");
        sb.AppendLine($"  {"إجمالي المرتجعات:",-40} {totalReturns,12:0.000} JOD");
        sb.AppendLine($"  {"صافي المبيعات:",-40} {totalSalesAll - totalReturns,12:0.000} JOD");
        sb.AppendLine();

        // ===================================================
        // FOOTER
        // ===================================================
        sb.AppendLine(separator);
        sb.AppendLine($"  تم إعداد التقرير في: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(userName))
            sb.AppendLine($"  بواسطة: {userName}");
        sb.AppendLine(separator);

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    #endregion
}