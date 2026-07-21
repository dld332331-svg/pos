namespace POS.Reporting.Reports;

/// <summary>
/// Comprehensive Arabic sales report builder with 4 report types:
/// Daily, By Category, By User, and By Payment Method.
/// </summary>
public class SaleReportBuilder : ISaleReportBuilder
{
    #region Daily Sales Report

    public byte[] BuildDailySalesReport(DateTime date, string businessName, DailySalesReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        var separator = new string('=', 75);
        var thinSep = new string('-', 75);

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                      تقرير المبيعات اليومية - {businessName}");
        sb.AppendLine($"                                    التاريخ: {date:yyyy/MM/dd}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // HOURLY BREAKDOWN
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                          التوزيع الساعي                                 │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────┤");

        if (data.HourlySales.Count > 0)
        {
            sb.AppendLine($"│  {"الساعة",-12} {"الإيرادات",15} {"عدد العمليات",12} {"متوسط الفاتورة",15}  │");
            sb.AppendLine($"│  {new string('-', 12)} {new string('-', 15)} {new string('-', 12)} {new string('-', 15)}  │");

            foreach (var h in data.HourlySales)
            {
                string hourLabel = $"{h.Hour:D2}:00 - {h.Hour + 1:D2}:00";
                sb.AppendLine($"│  {hourLabel,-12} {h.TotalSales,15:0.000} {h.TransactionCount,12} {h.AverageTicket,15:0.000}  │");
            }
        }
        else
        {
            sb.AppendLine("│  لا توجد مبيعات في هذا اليوم                                             │");
        }

        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // PAYMENT METHOD BREAKDOWN
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    المبيعات حسب طريقة الدفع");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.PaymentBreakdown.Count > 0)
        {
            sb.AppendLine($"  {"طريقة الدفع",-20} {"المبلغ",15} {"النسبة",10} {"عدد العمليات",12}");
            sb.AppendLine($"  {new string('-', 20)} {new string('-', 15)} {new string('-', 10)} {new string('-', 12)}");

            foreach (var pm in data.PaymentBreakdown)
            {
                sb.AppendLine($"  {pm.MethodName,-20} {pm.TotalAmount,15:0.000} JOD {pm.Percentage,9:0.00}% {pm.TransactionCount,12}");
            }
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات طرق دفع متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // TOP PRODUCTS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                      أعلى المنتجات مبيعاً");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.TopProducts.Count > 0)
        {
            sb.AppendLine($"  {"#",-4} {"اسم المنتج",-28} {"الكمية",10} {"الإيرادات",15} {"العمليات",10}");
            sb.AppendLine($"  {new string('-', 4)} {new string('-', 28)} {new string('-', 10)} {new string('-', 15)} {new string('-', 10)}");

            int rank = 1;
            foreach (var p in data.TopProducts)
            {
                sb.AppendLine($"  {rank,-4} {p.ProductName,-28} {p.QuantitySold,10:0.000} {p.TotalRevenue,15:0.000} JOD {p.TransactionCount,10}");
                rank++;
            }
        }
        else
        {
            sb.AppendLine("  لا توجد بيانات منتجات متاحة");
        }

        sb.AppendLine();

        // ===============================================================
        // REFUNDS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                         المرتجعات");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.Refunds.Count > 0)
        {
            sb.AppendLine($"  {"رقم العملية",-15} {"الوقت",-10} {"المنتج",-25} {"السبب",-15} {"المبلغ",12}");
            sb.AppendLine($"  {new string('-', 15)} {new string('-', 10)} {new string('-', 25)} {new string('-', 15)} {new string('-', 12)}");

            foreach (var r in data.Refunds)
            {
                sb.AppendLine($"  {r.TransactionId,-15} {r.Time:HH:mm,-10} {r.ProductName,-25} {r.Reason,-15} {r.Amount,12:0.000}");
            }

            sb.AppendLine(thinSep);
            var totalRefunds = data.Refunds.Sum(r => r.Amount);
            sb.AppendLine($"  {"إجمالي المرتجعات:",-65} {totalRefunds,12:0.000} JOD");
            sb.AppendLine($"  {"عدد عمليات الاسترجاع:",-65} {data.Refunds.Count,12}");
        }
        else
        {
            sb.AppendLine("  ✓ لا توجد مرتجعات في هذا اليوم");
        }

        sb.AppendLine();

        // ===============================================================
        // SUMMARY TOTALS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                          الملخص الإجمالي");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  إجمالي المبيعات:        {data.GrandTotal,18:0.000} JOD                         │");
        sb.AppendLine($"│  إجمالي الضريبة:         {data.GrandTax,18:0.000} JOD                         │");
        sb.AppendLine($"│  إجمالي الخصومات:        {data.GrandDiscount,18:0.000} JOD                         │");
        sb.AppendLine($"│  صافي المبيعات:          {data.NetSales,18:0.000} JOD                         │");
        sb.AppendLine($"│  عدد العمليات:           {data.TotalTransactions,18}                                │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
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

    #region Sales By Category Report

    public byte[] BuildSalesByCategoryReport(DateTime from, DateTime to, string businessName, SalesByCategoryReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        var separator = new string('=', 80);
        var thinSep = new string('-', 80);
        int days = (to - from).Days + 1;

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                    تقرير المبيعات حسب الفئة - {businessName}");
        sb.AppendLine($"                    الفترة: {from:yyyy/MM/dd} إلى {to:yyyy/MM/dd} ({days} يوم)");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // PER-CATEGORY SALES TABLE
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                              تفاصيل المبيعات حسب الفئة                                │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────────────────┤");

        if (data.Categories.Count > 0)
        {
            sb.AppendLine($"│  {"الفئة",-25} {"الكمية المباعة",15} {"الإيرادات",18} {"متوسط السعر",15} {"العمليات",10}   │");
            sb.AppendLine($"│  {new string('-', 25)} {new string('-', 15)} {new string('-', 18)} {new string('-', 15)} {new string('-', 10)}   │");

            foreach (var cat in data.Categories)
            {
                sb.AppendLine($"│  {cat.CategoryName,-25} {cat.QuantitySold,15:0.000} {cat.TotalRevenue,18:0.000} JOD {cat.AveragePrice,15:0.000} JOD {cat.TransactionCount,10}   │");
            }

            sb.AppendLine($"│  {thinSep.Substring(0, 72)}   │");

            // Category totals
            var totalQty = data.Categories.Sum(c => c.QuantitySold);
            var totalRevenue = data.Categories.Sum(c => c.TotalRevenue);
            var totalTx = data.Categories.Sum(c => c.TransactionCount);
            var avgAll = totalTx > 0 ? totalRevenue / totalTx : 0;

            sb.AppendLine($"│  {"الإجمالي",-25} {totalQty,15:0.000} {totalRevenue,18:0.000} JOD {avgAll,15:0.000} JOD {totalTx,10}   │");
        }
        else
        {
            sb.AppendLine("│  لا توجد بيانات فئات متاحة                                                            │");
        }

        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // REVENUE DISTRIBUTION BY CATEGORY (percentage)
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    توزيع الإيرادات حسب الفئة");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.Categories.Count > 0 && data.GrandTotal > 0)
        {
            sb.AppendLine($"  {"الفئة",-30} {"الإيرادات",15} {"النسبة من الإجمالي",18}");
            sb.AppendLine($"  {new string('-', 30)} {new string('-', 15)} {new string('-', 18)}");

            foreach (var cat in data.Categories)
            {
                decimal pct = (cat.TotalRevenue / data.GrandTotal) * 100;
                int barLen = (int)(pct / 2.5m);
                string bar = new('\u2588', barLen);

                sb.AppendLine($"  {cat.CategoryName,-30} {cat.TotalRevenue,15:0.000} JOD {pct,6:0.00}%  {bar}");
            }
        }

        sb.AppendLine();

        // ===============================================================
        // SUMMARY TOTALS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                          الملخص الإجمالي");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  إجمالي الإيرادات:        {data.GrandTotal,18:0.000} JOD                                             │");
        sb.AppendLine($"│  إجمالي العمليات:         {data.TotalTransactions,18}                                                          │");
        sb.AppendLine($"│  عدد الفئات:              {data.Categories.Count,18}                                                          │");
        sb.AppendLine($"│  متوسط الإيرادات اليومية: {(data.GrandTotal / Math.Max(days, 1)),18:0.000} JOD                                             │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
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

    #region Sales By User Report

    public byte[] BuildSalesByUserReport(DateTime from, DateTime to, string businessName, SalesByUserReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        var separator = new string('=', 80);
        var thinSep = new string('-', 80);
        int days = (to - from).Days + 1;

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                    تقرير المبيعات حسب الموظف - {businessName}");
        sb.AppendLine($"                    الفترة: {from:yyyy/MM/dd} إلى {to:yyyy/MM/dd} ({days} يوم)");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // PER-CASHIER PERFORMANCE TABLE
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                          أداء الموظفين                                            │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────────────────┤");

        if (data.Users.Count > 0)
        {
            sb.AppendLine($"│  {"الموظف",-20} {"العمليات",10} {"إجمالي المبيعات",18} {"متوسط الفاتورة",15} {"المرتجعات",18}   │");
            sb.AppendLine($"│  {new string('-', 20)} {new string('-', 10)} {new string('-', 18)} {new string('-', 15)} {new string('-', 18)}   │");

            foreach (var user in data.Users)
            {
                sb.AppendLine($"│  {user.UserName,-20} {user.TransactionCount,10} {user.TotalSales,18:0.000} JOD {user.AverageTicket,15:0.000} JOD {user.TotalRefunds,18:0.000} JOD   │");
            }

            sb.AppendLine($"│  {thinSep.Substring(0, 72)}   │");

            // Totals
            var totalTx = data.Users.Sum(u => u.TransactionCount);
            var totalSales = data.Users.Sum(u => u.TotalSales);
            var avgTicket = totalTx > 0 ? totalSales / totalTx : 0;
            var totalRefunds = data.Users.Sum(u => u.TotalRefunds);

            sb.AppendLine($"│  {"الإجمالي",-20} {totalTx,10} {totalSales,18:0.000} JOD {avgTicket,15:0.000} JOD {totalRefunds,18:0.000} JOD   │");
        }
        else
        {
            sb.AppendLine("│  لا توجد بيانات موظفين متاحة                                                         │");
        }

        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // PERFORMANCE RANKING
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                    ترتيب الموظفين حسب المبيعات");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.Users.Count > 0)
        {
            var ranked = data.Users.OrderByDescending(u => u.TotalSales).ToList();
            sb.AppendLine($"  {"#",4} {"الموظف",-20} {"إجمالي المبيعات",18} {"نسبة من الإجمالي",15} {"عدد العمليات",12}");
            sb.AppendLine($"  {new string('-', 4)} {new string('-', 20)} {new string('-', 18)} {new string('-', 15)} {new string('-', 12)}");

            int rank = 1;
            foreach (var user in ranked)
            {
                decimal pct = data.GrandTotal > 0 ? (user.TotalSales / data.GrandTotal) * 100 : 0;
                sb.AppendLine($"  {rank,4} {user.UserName,-20} {user.TotalSales,18:0.000} JOD {pct,14:0.00}% {user.TransactionCount,12}");
                rank++;
            }
        }

        sb.AppendLine();

        // ===============================================================
        // SUMMARY TOTALS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                          الملخص الإجمالي");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  إجمالي المبيعات:          {data.GrandTotal,18:0.000} JOD                                             │");
        sb.AppendLine($"│  إجمالي العمليات:           {data.TotalTransactions,18}                                                          │");
        sb.AppendLine($"│  عدد الموظفين النشطين:      {data.Users.Count,18}                                                          │");
        sb.AppendLine($"│  متوسط الفاتورة العامة:     {(data.TotalTransactions > 0 ? data.GrandTotal / data.TotalTransactions : 0),18:0.000} JOD                                             │");
        sb.AppendLine($"│  متوسط المبيعات اليومية:    {(data.GrandTotal / Math.Max(days, 1)),18:0.000} JOD                                             │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────────────────┘");
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

    #region Sales By Payment Method Report

    public byte[] BuildSalesByPaymentMethodReport(DateTime from, DateTime to, string businessName, SalesByPaymentMethodReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        var separator = new string('=', 75);
        var thinSep = new string('-', 75);
        int days = (to - from).Days + 1;

        // ===============================================================
        // HEADER
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine($"                  تقرير المبيعات حسب طريقة الدفع - {businessName}");
        sb.AppendLine($"                  الفترة: {from:yyyy/MM/dd} إلى {to:yyyy/MM/dd} ({days} يوم)");
        sb.AppendLine(separator);
        sb.AppendLine();

        // ===============================================================
        // PAYMENT METHOD BREAKDOWN
        // ===============================================================
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine("│                      تفاصيل طرق الدفع                                   │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────┤");

        if (data.Methods.Count > 0)
        {
            sb.AppendLine($"│  {"طريقة الدفع",-20} {"المبلغ",15} {"النسبة",10} {"عدد العمليات",12}  │");
            sb.AppendLine($"│  {new string('-', 20)} {new string('-', 15)} {new string('-', 10)} {new string('-', 12)}  │");

            decimal runningTotal = 0;
            foreach (var method in data.Methods)
            {
                runningTotal += method.TotalAmount;
                string percentageStr = data.GrandTotal > 0
                    ? $"{(method.TotalAmount / data.GrandTotal) * 100:0.00}%"
                    : "0.00%";

                // Visual bar
                int barLen = data.GrandTotal > 0
                    ? (int)((method.TotalAmount / data.GrandTotal) * 20)
                    : 0;
                string bar = new('\u2588', barLen);

                sb.AppendLine($"│  {method.MethodName,-20} {method.TotalAmount,15:0.000} JOD {percentageStr,9} {method.TransactionCount,12}  │");
                sb.AppendLine($"│  {bar,-72}  │");
            }

            sb.AppendLine($"│  {thinSep.Substring(0, 65)}  │");
            sb.AppendLine($"│  {"الإجمالي",-20} {data.GrandTotal,15:0.000} JOD {"100.00%",9} {data.TotalTransactions,12}  │");
        }
        else
        {
            sb.AppendLine("│  لا توجد بيانات طرق دفع متاحة                                             │");
        }

        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // CASH VS CARD COMPARISON
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("              مقارنة النقدي مقابل البطاقة");
        sb.AppendLine(separator);
        sb.AppendLine();

        var cashMethod = data.Methods.FirstOrDefault(m =>
            m.MethodName.Contains("نقدي") || m.MethodName.Contains("كاش") || m.MethodName.Contains("Cash"));
        var cardMethod = data.Methods.FirstOrDefault(m =>
            m.MethodName.Contains("بطاق") || m.MethodName.Contains("Card") || m.MethodName.Contains("فيزا") || m.MethodName.Contains("Visa"));

        decimal cashTotal = cashMethod?.TotalAmount ?? 0;
        decimal cardTotal = cardMethod?.TotalAmount ?? 0;
        int cashTx = cashMethod?.TransactionCount ?? 0;
        int cardTx = cardMethod?.TransactionCount ?? 0;
        decimal cashPct = data.GrandTotal > 0 ? (cashTotal / data.GrandTotal) * 100 : 0;
        decimal cardPct = data.GrandTotal > 0 ? (cardTotal / data.GrandTotal) * 100 : 0;

        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  النقدي:                                                                  │");
        sb.AppendLine($"│    المبلغ:                {cashTotal,15:0.000} JOD                                 │");
        sb.AppendLine($"│    النسبة:                {cashPct,15:0.00} %                                   │");
        sb.AppendLine($"│    عدد العمليات:          {cashTx,15}                                          │");
        sb.AppendLine($"│    متوسط الفاتورة:        {(cashTx > 0 ? cashTotal / cashTx : 0),15:0.000} JOD                                 │");
        sb.AppendLine("├──────────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│  البطاقة:                                                                  │");
        sb.AppendLine($"│    المبلغ:                {cardTotal,15:0.000} JOD                                 │");
        sb.AppendLine($"│    النسبة:                {cardPct,15:0.00} %                                   │");
        sb.AppendLine($"│    عدد العمليات:          {cardTx,15}                                          │");
        sb.AppendLine($"│    متوسط الفاتورة:        {(cardTx > 0 ? cardTotal / cardTx : 0),15:0.000} JOD                                 │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine();

        // ===============================================================
        // VISUAL DISTRIBUTION
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                   التوزيع البياني");
        sb.AppendLine(separator);
        sb.AppendLine();

        if (data.Methods.Count > 0 && data.GrandTotal > 0)
        {
            int totalBarWidth = 50;
            foreach (var method in data.Methods.OrderByDescending(m => m.TotalAmount))
            {
                int barLen = Math.Max(1, (int)((method.TotalAmount / data.GrandTotal) * totalBarWidth));
                string bar = new('\u2588', barLen);
                decimal pct = (method.TotalAmount / data.GrandTotal) * 100;
                sb.AppendLine($"  {method.MethodName,-15} {bar} {pct,6:0.00}% ({method.TotalAmount:0.000} JOD)");
            }
        }

        sb.AppendLine();

        // ===============================================================
        // SUMMARY TOTALS
        // ===============================================================
        sb.AppendLine(separator);
        sb.AppendLine("                          الملخص الإجمالي");
        sb.AppendLine(separator);
        sb.AppendLine();
        sb.AppendLine("┌──────────────────────────────────────────────────────────────────────────ف");
        sb.AppendLine($"│  إجمالي المبيعات:          {data.GrandTotal,18:0.000} JOD                         │");
        sb.AppendLine($"│  إجمالي العمليات:           {data.TotalTransactions,18}                                │");
        sb.AppendLine($"│  عدد طرق الدفع المستخدمة:  {data.Methods.Count,18}                                │");
        sb.AppendLine($"│  متوسط المبيعات اليومية:    {(data.GrandTotal / Math.Max(days, 1)),18:0.000} JOD                         │");
        sb.AppendLine("└──────────────────────────────────────────────────────────────────────────┘");
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