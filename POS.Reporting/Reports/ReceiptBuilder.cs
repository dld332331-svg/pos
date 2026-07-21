namespace POS.Reporting.Reports;

public class ReceiptBuilder
{
    private const int ReceiptWidth = 48;

    public string BuildReceipt(ReceiptData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CenterText(data.BusinessName));
        sb.AppendLine(CenterText(data.Address ?? ""));
        sb.AppendLine(CenterText(data.Phone ?? ""));
        sb.AppendLine(new string('-', ReceiptWidth));
        sb.AppendLine($"رقم الفاتورة: {data.InvoiceNumber}");
        sb.AppendLine($"التاريخ: {data.Date:yyyy/MM/dd HH:mm}");
        sb.AppendLine($"الكاشير: {data.CashierName}");
        if (!string.IsNullOrEmpty(data.CustomerName))
            sb.AppendLine($"العميل: {data.CustomerName}");
        if (data.TableNumber.HasValue)
            sb.AppendLine($"الطاولة: {data.TableNumber}");
        sb.AppendLine(new string('-', ReceiptWidth));
        sb.AppendLine($"{RightAlign("الكمية", 6)} {RightAlign("السعر", 10)} {RightAlign("المجموع", 10)} {"الصنف"}");
        sb.AppendLine(new string('-', ReceiptWidth));
        foreach (var item in data.Items)
        {
            sb.AppendLine(item.ProductName);
            sb.AppendLine($"{RightAlign(item.Quantity.ToString("0.000"), 6)} {RightAlign(item.UnitPrice.ToString("0.000"), 10)} {RightAlign(item.LineTotal.ToString("0.000"), 10)}");
        }
        sb.AppendLine(new string('-', ReceiptWidth));
        sb.AppendLine($"{"المجموع الفرعي:",-28} {data.SubTotal,10:0.000}");
        sb.AppendLine($"{"الضريبة:",-28} {data.TaxAmount,10:0.000}");
        if (data.DiscountAmount > 0)
            sb.AppendLine($"{"الخصم:",-28} {-data.DiscountAmount,10:0.000}");
        sb.AppendLine(new string('=', ReceiptWidth));
        sb.AppendLine($"{"الإجمالي:",-28} {data.TotalAmount,10:0.000} JOD");
        sb.AppendLine(new string('-', ReceiptWidth));
        foreach (var p in data.Payments)
            sb.AppendLine($"{p.Method,-15} {p.Amount,10:0.000}");
        sb.AppendLine(new string('-', ReceiptWidth));
        sb.AppendLine(CenterText("شكراً لزيارتكم"));
        sb.AppendLine(CenterText(data.Footer ?? ""));
        return sb.ToString();
    }

    private string CenterText(string text) => CenterText(text, ReceiptWidth);
    private string CenterText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return new string(' ', width);
        int pad = Math.Max(0, (width - text.Length) / 2);
        return text.PadLeft(pad + text.Length).PadRight(width);
    }
    private string RightAlign(string text, int width) => text.PadLeft(width);
}

public class ReceiptData
{
    public string BusinessName { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public string CashierName { get; set; } = "";
    public string? CustomerName { get; set; }
    public int? TableNumber { get; set; }
    public List<ReceiptItem> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ReceiptPayment> Payments { get; set; } = new();
    public string? Footer { get; set; }
}
public class ReceiptItem { public string ProductName { get; set; } = ""; public decimal Quantity { get; set; } public decimal UnitPrice { get; set; } public decimal LineTotal { get; set; } }
public class ReceiptPayment { public string Method { get; set; } = ""; public decimal Amount { get; set; } }