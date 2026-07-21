namespace POS.Reporting.Reports;
using POS.Application.DTOs;

#region Supporting Records

public record CategoryStockSummaryDto(string CategoryName, int ProductCount, decimal TotalQuantity, decimal TotalValue);
public record StockMovementSummaryDto(decimal PurchasesIn, decimal SalesOut, decimal ReturnsIn, decimal WasteOut, decimal Adjustments);

#endregion

/// <summary>
/// Builds comprehensive Arabic inventory reports: current stock, movements,
/// low stock alerts, zero stock, stock values, and category breakdowns.
/// </summary>
public class InventoryReportBuilder
{
    #region Current Stock Report

    public byte[] BuildCurrentStockReport(
        List<InventoryStatusDto> items,
        string businessName,
        List<CategoryStockSummaryDto>? categorySummaries = null,
        StockMovementSummaryDto? movementSummary = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var sb = new System.Text.StringBuilder();
        categorySummaries ??= new List<CategoryStockSummaryDto>();
        movementSummary ??= new StockMovementSummaryDto(0, 0, 0, 0, 0);

        var separator = new string('=', 100);
        var thinSep = new string('-', 100);

        var lowStockItems = items.Where(i => i.IsLowStock).ToList();
        var zeroStockItems = items.Where(i => i.Quantity <= 0).ToList();

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                           تقرير المخزون الحالي - {businessName}");
        sb.AppendLine($"                                     التاريخ: {DateTime.Now:yyyy/MM/dd HH:mm}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // EXECUTIVE SUMMARY
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                              الملخص التنفيذي للمخزون                                │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│  إجمالي المنتجات:                 {items.Count,8} منتج                                        │");
        sb.AppendLine($"│  منتجات منخفضة المخزون:          {lowStockItems.Count,8} منتج                                        │");
        sb.AppendLine($"│  منتجات نفذت من المخزون:          {zeroStockItems.Count,8} منتج                                        │");
        sb.AppendLine($"│  منتجات بمخزون كافف:              {items.Count - lowStockItems.Count,8} منتج                                        │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // DETAILED STOCK TABLE
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                            جدول المخزون التفصيلي");
        sb.AppendLine(separator);
        sb.AppendLine();

        sb.AppendLine($"  {"اسم المنتج",-30} {"الكمية الحالية",12} {"المحجوزة",10} {"المتاحة",10} {"الحد الأدنى",10} {"الوحدة",8} {"الحالة",10}");
        sb.AppendLine($"  {new string('-', 30)} {new string('-', 12)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 8)} {new string('-', 10)}");

        foreach (var item in items)
        {
            string status;
            if (item.Quantity <= 0)
                status = "نفذ";
            else if (item.IsLowStock)
                status = "منخفض";
            else
                status = "طبيعي";

            sb.AppendLine($"  {item.ProductName,-30} {item.Quantity,12:0.000} {item.ReservedQuantity,10:0.000} {item.AvailableQuantity,10:0.000} {item.MinStock,10:0.000} {item.Unit,-8} {status,-10}");
        }

        sb.AppendLine();

        // ===============================================================
        // LOW STOCK ALERTS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    ⚠ تنبيهات المخزون المنخفض");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (lowStockItems.Count > 0)
        {
            sb.AppendLine($"  {"اسم المنتج",-30} {"الكمية الحالية",12} {"الحد الأدنى",12} {"النقص",12} {"الوحدة",8}");
            sb.AppendLine($"  {new string('-', 30)} {new string('-', 12)} {new string('-', 12)} {new string('-', 12)} {new string('-', 8)}");

            foreach (var item in lowStockItems)
            {
                decimal shortage = item.MinStock - item.Quantity;
                sb.AppendLine($"  {item.ProductName,-30} {item.Quantity,12:0.000} {item.MinStock,12:0.000} {shortage,12:0.000} {item.Unit,-8}");
            }

            sb.AppendLine();
            sb.AppendLine($"  إجمالي المنتجات المنخفضة: {lowStockItems.Count}");
        }
        else
        {
            sb.AppendLine("  ✓ لا توجد منتجات بمخزون منخفض - جميع المنتجات فوق الحد الأدنى");
        }

        sb.AppendLine();

        // ===============================================================
        // ZERO STOCK ITEMS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    ✗ المنتجات النافدة من المخزون");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (zeroStockItems.Count > 0)
        {
            sb.AppendLine($"  {"اسم المنتج",-40} {"الوحدة",-10}");
            sb.AppendLine($"  {new string('-', 40)} {new string('-', 10)}");

            foreach (var item in zeroStockItems)
            {
                sb.AppendLine($"  {item.ProductName,-40} {item.Unit,-10}");
            }

            sb.AppendLine();
            sb.AppendLine($"  إجمالي المنتجات النافدة: {zeroStockItems.Count}");
        }
        else
        {
            sb.AppendLine("  ✓ لا توجد منتجات نافدة من المخزون");
        }

        sb.AppendLine();

        // ===============================================================
        // STOCK VALUE SUMMARY
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                      قيمة المخزون");
        sb.AppendLine(separator);
        sb.AppendLine();

        var totalQuantity = items.Sum(i => i.Quantity);
        var totalAvailable = items.Sum(i => i.AvailableQuantity);
        var totalReserved = items.Sum(i => i.ReservedQuantity);

        sb.AppendLine($"  إجمالي الكمية في المخزون:        {totalQuantity,15:0.000} وحدة");
        sb.AppendLine($"  إجمالي الكمية المتاحة:           {totalAvailable,15:0.000} وحدة");
        sb.AppendLine($"  إجمالي الكمية المحجوزة:          {totalReserved,15:0.000} وحدة");
        sb.AppendLine();

        // ===============================================================
        // MOVEMENT SUMMARY
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    ملخص حركات المخزون");
        sb.AppendLine(separator);
        sb.AppendLine();

        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  المشتريات الواردة:       {movementSummary.PurchasesIn,18:0.000} وحدة                             │");
        sb.AppendLine($"│  المبيعات الصادرة:       {movementSummary.SalesOut,18:0.000} وحدة                             │");
        sb.AppendLine($"│  المرتجعات الواردة:      {movementSummary.ReturnsIn,18:0.000} وحدة                             │");
        sb.AppendLine($"│  التالف والهدر:          {movementSummary.WasteOut,18:0.000} وحدة                             │");
        sb.AppendLine($"│  التسويات:               {movementSummary.Adjustments,18:0.000} وحدة                             │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // CATEGORY-WISE STOCK SUMMARY
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                 ملخص المخزون حسب الفئة");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (categorySummaries.Count > 0)
        {
            sb.AppendLine($"  {"الفئة",-30} {"عدد المنتجات",12} {"إجمالي الكمية",15} {"القيمة الإجمالية",18}");
            sb.AppendLine($"  {new string('-', 30)} {new string('-', 12)} {new string('-', 15)} {new string('-', 18)}");

            foreach (var cat in categorySummaries)
            {
                sb.AppendLine($"  {cat.CategoryName,-30} {cat.ProductCount,12} {cat.TotalQuantity,15:0.000} {cat.TotalValue,18:0.000} JOD");
            }

            sb.AppendLine(thinSep);

            var totalProducts = categorySummaries.Sum(c => c.ProductCount);
            var totalQty = categorySummaries.Sum(c => c.TotalQuantity);
            var totalValue = categorySummaries.Sum(c => c.TotalValue);

            sb.AppendLine($"  {"الإجمالي",-30} {totalProducts,12} {totalQty,15:0.000} {totalValue,18:0.000} JOD");
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات فئات متاحة");
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

    #region Movements Report

    public byte[] BuildMovementsReport(List<InventoryMovementDto> movements, string businessName)
    {
        ArgumentNullException.ThrowIfNull(movements);
        var sb = new System.Text.StringBuilder();
        var separator = new string('=', 110);
        var thinSep = new string('-', 110);

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                        تقرير حركات المخزون - {businessName}");
        sb.AppendLine($"                                       التاريخ: {DateTime.Now:yyyy/MM/dd HH:mm}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // MOVEMENT SUMMARY STATS
        // ===============================================================
        var grouped = movements.GroupBy(m => m.MovementType).ToDictionary(g => g.Key, g => g.ToList());

        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                             ملخص الحركات                                            │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│  إجمالي الحركات:                      {movements.Count,8} حركة                                         │");
        sb.AppendLine($"│  مشتريات واردة:                      {grouped.GetValueOrDefault("شراء", new List<InventoryMovementDto>()).Count,8} حركة                                         │");
        sb.AppendLine($"│  مبيعات صادرة:                      {grouped.GetValueOrDefault("بيع", new List<InventoryMovementDto>()).Count,8} حركة                                         │");
        sb.AppendLine($"│  مرتجعات واردة:                     {grouped.GetValueOrDefault("مرتجع", new List<InventoryMovementDto>()).Count,8} حركة                                         │");
        sb.AppendLine($"│  تالف / هدر:                        {grouped.GetValueOrDefault("تالف", new List<InventoryMovementDto>()).Count,8} حركة                                         │");
        sb.AppendLine($"│  تسويات:                            {grouped.GetValueOrDefault("تسوية", new List<InventoryMovementDto>()).Count,8} حركة                                         │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // DETAILED MOVEMENT TABLE
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                       تفاصيل الحركات");
        sb.AppendLine(separator);
        sb.AppendLine();

        sb.AppendLine($"  {"التاريخ",-18} {"اسم المنتج",-22} {"نوع الحركة",10} {"الكمية",10} {"قبل",10} {"بعد",10} {"المستخدم",-12} {"السبب",-18}");
        sb.AppendLine($"  {new string('-', 18)} {new string('-', 22)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 12)} {new string('-', 18)}");

        foreach (var m in movements)
        {
            sb.AppendLine($"  {m.Timestamp:yyyy/MM/dd HH:mm,-18} {m.ProductName,-22} {m.MovementType,-10} {m.Quantity,10:0.000} {m.BeforeQuantity,10:0.000} {m.AfterQuantity,10:0.000} {m.UserName,-12} {(m.Reason ?? "-"),-18}");
        }

        sb.AppendLine();

        // ===============================================================
        // MOVEMENTS BY TYPE SUMMARY
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                   ملخص الحركات حسب النوع");
        sb.AppendLine(separator);
        sb.AppendLine();

        sb.AppendLine($"  {"نوع الحركة",-15} {"عدد الحركات",12} {"إجمالي الكمية",15}");
        sb.AppendLine($"  {new string('-', 15)} {new string('-', 12)} {new string('-', 15)}");

        foreach (var group in movements.GroupBy(m => m.MovementType).OrderBy(g => g.Key))
        {
            sb.AppendLine($"  {group.Key,-15} {group.Count(),12} {group.Sum(m => m.Quantity),15:0.000}");
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