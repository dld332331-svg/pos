namespace POS.Reporting.Reports;
using POS.Application.DTOs;

#region Supporting Records

public record BottomProductDto(string ProductName, decimal QuantitySold, decimal Revenue, decimal Cost, decimal Profit, decimal Margin);
public record CategoryProfitDto(string CategoryName, decimal Revenue, decimal Cost, decimal Profit, decimal Margin);
public record DailyProfitDto(DateTime Date, decimal Revenue, decimal Cost, decimal Profit, decimal Margin);

#endregion

/// <summary>
/// Builds a comprehensive Arabic profitability report with executive summary,
/// top/bottom products, category breakdown, and daily trend.
/// </summary>
public class ProfitabilityReportBuilder
{
    #region Main Report Method

    public byte[] BuildProfitabilityReport(
        ProfitabilityReportDto data,
        string businessName,
        DateTime? from,
        DateTime? to,
        List<BottomProductDto>? bottomProducts = null,
        List<CategoryProfitDto>? categoryProfits = null,
        List<DailyProfitDto>? dailyProfits = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        bottomProducts ??= new List<BottomProductDto>();
        categoryProfits ??= new List<CategoryProfitDto>();
        dailyProfits ??= new List<DailyProfitDto>();

        var separator = new string('=', 80);
        var thinSep = new string('-', 80);
        var dotSep = new string('.', 80);

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                          تقرير الربحية - {businessName}");
        sb.AppendLine($"                    الفترة: {from:yyyy/MM/dd} - {to:yyyy/MM/dd}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // EXECUTIVE SUMMARY
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                           الملخص التنفيذي                                 │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│  إجمالي المبيعات:        {data.TotalSales,18:0.000} JOD                         │");
        sb.AppendLine($"│  إجمالي التكلفة:         {data.TotalCost,18:0.000} JOD                         │");
        sb.AppendLine($"│  إجمالي الربح:           {data.GrossProfit,18:0.000} JOD                         │");
        sb.AppendLine($"│  هامش الربح الإجمالي:   {data.ProfitMargin,18:0.000} %                            │");

        decimal netMargin = data.TotalSales > 0 ? (data.GrossProfit / data.TotalSales) * 100 : 0;
        sb.AppendLine($"│  صافي هامش الربح:       {netMargin,18:0.000} %                            │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // COST RATIO ANALYSIS
        // ===============================================================
        sb.AppendLine(dotSep);
        sb.AppendLine("                       تحليل نسبة التكلفة");
        sb.AppendLine(dotSep);
        sb.AppendLine();

        decimal costRatio = data.TotalSales > 0 ? (data.TotalCost / data.TotalSales) * 100 : 0;
        decimal profitRatio = 100 - costRatio;

        int barWidth = 40;
        int costBarLen = (int)(barWidth * costRatio / 100);
        int profitBarLen = barWidth - costBarLen;

        sb.AppendLine($"  نسبة التكلفة: {costRatio,6:0.00}%  [{"\u2588".PadRight(costBarLen, '\u2588')}{"\u2591".PadRight(profitBarLen, '\u2591')}]");
        sb.AppendLine($"  نسبة الربح:   {profitRatio,6:0.00}%  [{"\u2591".PadRight(costBarLen, '\u2591')}{"\u2588".PadRight(profitBarLen, '\u2588')}]");
        sb.AppendLine();

        // ===============================================================
        // TOP 10 PROFITABLE PRODUCTS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("              أعلى 10 منتجات ربحية");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.TopProducts.Count > 0)
        {
            sb.AppendLine($"  {"#",-4} {"اسم المنتج",-25} {"الكمية المباعة",12} {"الإيرادات",12} {"التكلفة",12} {"الربح",12} {"الهامش",8}");
            sb.AppendLine($"  {new string('-', 4)} {new string('-', 25)} {new string('-', 12)} {new string('-', 12)} {new string('-', 12)} {new string('-', 12)} {new string('-', 8)}");

            int rank = 1;
            foreach (var p in data.TopProducts.Take(10))
            {
                sb.AppendLine($"  {rank,-4} {p.ProductName,-25} {p.Sales,12:0.000} {p.Cost,12:0.000} {p.Profit,12:0.000} {p.Margin,8:0.00}%");
                rank++;
            }
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات منتجات متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // BOTTOM 10 PRODUCTS BY PROFIT
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("              أدنى 10 منتجات ربحية");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (bottomProducts.Count > 0)
        {
            sb.AppendLine($"  {"#",-4} {"اسم المنتج",-25} {"الكمية المباعة",12} {"الإيرادات",12} {"التكلفة",12} {"الربح",12} {"الهامش",8}");
            sb.AppendLine($"  {new string('-', 4)} {new string('-', 25)} {new string('-', 12)} {new string('-', 12)} {new string('-', 12)} {new string('-', 12)} {new string('-', 8)}");

            int rank = 1;
            foreach (var p in bottomProducts.Take(10))
            {
                string profitSign = p.Profit < 0 ? "-" : "";
                sb.AppendLine($"  {rank,-4} {p.ProductName,-25} {p.QuantitySold,12:0.000} {p.Revenue,12:0.000} {p.Cost,12:0.000} {p.Profit,12:0.000} {p.Margin,8:0.00}%");
                rank++;
            }
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات منتجات متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // CATEGORY PROFITABILITY BREAKDOWN
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                  ربحية الفئات");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (categoryProfits.Count > 0)
        {
            sb.AppendLine($"  {"الفئة",-25} {"الإيرادات",15} {"التكلفة",15} {"الربح",15} {"الهامش",10}");
            sb.AppendLine($"  {new string('-', 25)} {new string('-', 15)} {new string('-', 15)} {new string('-', 15)} {new string('-', 10)}");

            foreach (var cat in categoryProfits)
            {
                sb.AppendLine($"  {cat.CategoryName,-25} {cat.Revenue,15:0.000} {cat.Cost,15:0.000} {cat.Profit,15:0.000} {cat.Margin,10:0.00}%");
            }

            sb.AppendLine(thinSep);

            var totalCatRevenue = categoryProfits.Sum(c => c.Revenue);
            var totalCatCost = categoryProfits.Sum(c => c.Cost);
            var totalCatProfit = categoryProfits.Sum(c => c.Profit);
            var avgMargin = categoryProfits.Count > 0 ? categoryProfits.Average(c => c.Margin) : 0;

            sb.AppendLine($"  {"الإجمالي",-25} {totalCatRevenue,15:0.000} {totalCatCost,15:0.000} {totalCatProfit,15:0.000} {avgMargin,10:0.00}%");
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات فئات متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // DAILY PROFITABILITY TREND
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                   الاتجاه اليومي للربحية");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (dailyProfits.Count > 0)
        {
            sb.AppendLine($"  {"التاريخ",-12} {"الإيرادات",15} {"التكلفة",15} {"الربح",15} {"الهامش",10}");
            sb.AppendLine($"  {new string('-', 12)} {new string('-', 15)} {new string('-', 15)} {new string('-', 15)} {new string('-', 10)}");

            foreach (var day in dailyProfits)
            {
                sb.AppendLine($"  {day.Date:yyyy/MM/dd,-12} {day.Revenue,15:0.000} {day.Cost,15:0.000} {day.Profit,15:0.000} {day.Margin,10:0.00}%");
            }

            sb.AppendLine(thinSep);

            var totalRevenue = dailyProfits.Sum(d => d.Revenue);
            var totalCost = dailyProfits.Sum(d => d.Cost);
            var totalProfit = dailyProfits.Sum(d => d.Profit);
            var avgDayMargin = dailyProfits.Count > 0 ? dailyProfits.Average(d => d.Margin) : 0;

            sb.AppendLine($"  {"الإجمالي",-12} {totalRevenue,15:0.000} {totalCost,15:0.000} {totalProfit,15:0.000} {avgDayMargin,10:0.00}%");
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات يومية متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // FOOTER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"  تم إعداد التقرير في: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        sb.AppendLine($"  نظام نقاط البيع - {businessName}");
        sb.AppendLine(separator);

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    #endregion
}